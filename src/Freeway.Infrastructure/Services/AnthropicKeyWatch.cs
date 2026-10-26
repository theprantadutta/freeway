using Freeway.Domain.Entities;
using Freeway.Domain.Interfaces;
using Freeway.Infrastructure.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Freeway.Infrastructure.Services;

/// <summary>
/// Keeps track of the Anthropic key and emails before it expires.
///
/// The key cannot report its own expiry (that takes the Admin API, which a workspace
/// key is refused), so this does the next best thing: it records the first time it
/// sees each key, by fingerprint, and expects it to last ANTHROPIC_KEY_LIFETIME_DAYS.
/// Rotating the key is then just deploying the new one -- a new fingerprint starts a
/// new clock. The expiry can be corrected on the dashboard. And because an estimate
/// can be wrong, the provider actually refusing the key is reported too, straight
/// away, whatever the date says.
/// </summary>
public class AnthropicKeyWatch
{
    private const string Provider = AnthropicCreditService.ProviderName;

    // A key refused again and again (the credit retries every 15 minutes) is one
    // problem, not one an hour; repeat the email at most this often.
    private static readonly TimeSpan RejectionRepeat = TimeSpan.FromHours(12);

    private readonly IAppDbContext _db;
    private readonly AnthropicCreditLedger _ledger;
    private readonly IEmailSender _email;
    private readonly IDateTimeService _clock;
    private readonly ILogger<AnthropicKeyWatch> _logger;

    public AnthropicKeyWatch(
        IAppDbContext db,
        AnthropicCreditLedger ledger,
        IEmailSender email,
        IDateTimeService clock,
        ILogger<AnthropicKeyWatch> logger)
    {
        _db = db;
        _ledger = ledger;
        _email = email;
        _clock = clock;
        _logger = logger;
    }

    private static AnthropicCreditService.Settings Config => AnthropicCreditService.Config;

    /// <summary>The record for the key in use, created the first time the key is seen.</summary>
    public async Task<ProviderKey?> CurrentAsync(CancellationToken cancellationToken = default)
    {
        if (Config.ApiKey.Length == 0)
        {
            return null;
        }

        var now = _clock.UtcNow;
        var fingerprint = AnthropicCreditService.Fingerprint(Config.ApiKey);

        var key = await _db.ProviderKeys
            .FirstOrDefaultAsync(k => k.Provider == Provider && k.Fingerprint == fingerprint, cancellationToken);

        if (key is null)
        {
            key = new ProviderKey
            {
                Id = Guid.NewGuid(),
                Provider = Provider,
                Fingerprint = fingerprint,
                Hint = AnthropicCreditService.KeyHint,
                FirstSeenAt = now,
                LastSeenAt = now,
                ExpiresAt = Config.KeyLifetimeDays > 0 ? now.AddDays(Config.KeyLifetimeDays) : null,
                ExpirySource = "estimated"
            };
            _db.ProviderKeys.Add(key);
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "New Anthropic key {Hint} first seen; expected to expire {Expiry:u}",
                key.Hint, key.ExpiresAt);
            return key;
        }

        // Touch at most hourly; this runs every few minutes.
        if (now - key.LastSeenAt > TimeSpan.FromHours(1))
        {
            key.LastSeenAt = now;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return key;
    }

    /// <summary>Sends whichever reminder is due, once. Safe to run as often as you like.</summary>
    public async Task CheckAsync(CancellationToken cancellationToken = default)
    {
        var key = await CurrentAsync(cancellationToken);
        if (key is null)
        {
            return;
        }

        var now = _clock.UtcNow;

        // A refusal seen by a request since the last check.
        var (rejectedAt, rejection) = _ledger.KeyRejection;
        if (rejectedAt is { } at && (key.LastRejectedAt is null || at > key.LastRejectedAt))
        {
            key.LastRejectedAt = at;
            key.LastRejection = rejection;
            await _db.SaveChangesAsync(cancellationToken);
        }

        if (!Config.KeyReminders)
        {
            return;
        }

        if (!_email.IsConfigured)
        {
            _logger.LogDebug("Anthropic key reminders skipped: SMTP is not configured");
            return;
        }

        if (key.LastRejectedAt is { } refused &&
            refused > (key.RejectedNoticeAt ?? DateTime.MinValue) &&
            now - (key.RejectedNoticeAt ?? DateTime.MinValue) > RejectionRepeat)
        {
            if (await SendAsync(Rejected(key)))
            {
                key.RejectedNoticeAt = now;
            }
        }
        else if (key.ExpiresAt is { } expires && now >= expires && key.ExpiredNoticeAt is null)
        {
            if (await SendAsync(Expired(key)))
            {
                key.ExpiredNoticeAt = now;
                // An expired key no longer needs the advance warning.
                key.WarnedAt ??= now;
            }
        }
        else if (key.ExpiresAt is { } due && now >= due.AddDays(-Config.KeyWarnDays) && now < due && key.WarnedAt is null)
        {
            if (await SendAsync(Expiring(key, due - now)))
            {
                key.WarnedAt = now;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Sends the advance reminder for the current key now, whatever the date. For testing the mail.</summary>
    public async Task<bool> SendTestReminderAsync(CancellationToken cancellationToken = default)
    {
        var key = await CurrentAsync(cancellationToken);
        if (key is null || !_email.IsConfigured)
        {
            return false;
        }

        var left = (key.ExpiresAt ?? _clock.UtcNow) - _clock.UtcNow;
        return await SendAsync(Expiring(key, left), test: true);
    }

    /// <summary>Sets the expiry by hand and re-arms the reminders for it.</summary>
    public async Task<ProviderKey?> SetExpiryAsync(DateTime? expiresAt, CancellationToken cancellationToken = default)
    {
        var key = await CurrentAsync(cancellationToken);
        if (key is null)
        {
            return null;
        }

        key.ExpiresAt = expiresAt;
        key.ExpirySource = "manual";

        // A new date gets its own reminders, unless it is already past the point
        // where one was due and sent.
        var now = _clock.UtcNow;
        if (expiresAt is null || expiresAt > now.AddDays(Config.KeyWarnDays)) key.WarnedAt = null;
        if (expiresAt is null || expiresAt > now) key.ExpiredNoticeAt = null;

        await _db.SaveChangesAsync(cancellationToken);
        return key;
    }

    private async Task<bool> SendAsync(SpendAlert alert, bool test = false)
    {
        var alerts = new List<SpendAlert> { alert };
        var subject = (test ? "[Test] " : "") + EmailTemplates.AlertSubject(alerts);
        var sent = await _email.SendAsync(subject, EmailTemplates.AlertHtml(alerts), EmailTemplates.AlertText(alerts));

        if (sent)
            _logger.LogInformation("Sent Anthropic key reminder: {Title}", alert.Title);
        else
            _logger.LogWarning("Anthropic key reminder could not be sent: {Title}", alert.Title);

        return sent;
    }

    private const string RotateSteps =
        "In platform.claude.com, open API keys, create a new key in the same workspace, replace ANTHROPIC_API_KEY in the " +
        "server's .env, and run docker compose up -d. Freeway notices the new key by itself and starts a fresh clock. " +
        "Then delete the old key.";

    private static SpendAlert Expiring(ProviderKey key, TimeSpan left) => new()
    {
        Key = $"anthropic-key-expiring:{key.Fingerprint}",
        Severity = AlertSeverity.Warning,
        Title = $"Anthropic API key expires in {Days(left)}",
        Detail = $"The key {key.Hint}, first seen {key.FirstSeenAt:d MMM yyyy}, expires on {key.ExpiresAt:dddd d MMM yyyy} " +
                 $"({(key.ExpirySource == "manual" ? "date set on the dashboard" : "estimated from a " + Config.KeyLifetimeDays + "-day lifetime")}). " +
                 "Once it stops working, paid requests fall back to the Claude Code bridge and then OpenRouter until it is replaced.",
        Action = RotateSteps
    };

    private static SpendAlert Expired(ProviderKey key) => new()
    {
        Key = $"anthropic-key-expired:{key.Fingerprint}",
        Severity = AlertSeverity.Critical,
        Title = "Anthropic API key has expired",
        Detail = $"The key {key.Hint} was due to expire on {key.ExpiresAt:d MMM yyyy}. The monthly Claude credit is not being used " +
                 "until a new key is in place; requests are being served by the Claude Code bridge and OpenRouter instead.",
        Action = RotateSteps
    };

    private static SpendAlert Rejected(ProviderKey key) => new()
    {
        Key = $"anthropic-key-rejected:{key.Fingerprint}",
        Severity = AlertSeverity.Critical,
        Title = "Anthropic rejected the API key",
        Detail = $"The key {key.Hint} was refused at {key.LastRejectedAt:u}: {key.LastRejection}. It may have expired, been " +
                 "revoked, or been deleted. The credit is paused and requests are falling back to the bridge and OpenRouter.",
        Action = RotateSteps
    };

    private static string Days(TimeSpan span)
    {
        var days = (int)Math.Ceiling(Math.Max(0, span.TotalDays));
        return days switch
        {
            0 => "less than a day",
            1 => "1 day",
            _ => $"{days} days"
        };
    }
}

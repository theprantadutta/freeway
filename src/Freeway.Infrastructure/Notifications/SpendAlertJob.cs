using Freeway.Domain.Interfaces;
using Freeway.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Freeway.Infrastructure.Notifications;

public interface ISpendAlertJob
{
    Task CheckAsync();

    /// <summary>Runs the checks and returns what fired, without the cooldown filter. For the admin endpoint.</summary>
    Task<List<SpendAlert>> EvaluateAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Watches for spend that does not look like normal use.
///
/// The practical motivation is a leaked project key: the gateway does not enforce
/// rate limits, so the first signal of abuse is a spend or request-count spike.
/// Thresholds are absolute rather than relative on purpose — a baseline-relative
/// rule stays quiet when a project is busy from its very first day, which is
/// exactly the leaked-key case.
/// </summary>
public class SpendAlertJob : ISpendAlertJob
{
    private readonly AppDbContext _context;
    private readonly IOpenRouterService _openRouterService;
    private readonly IEmailSender _email;
    private readonly IAlertCooldownCache _cooldown;
    private readonly ILogger<SpendAlertJob> _logger;

    public SpendAlertJob(
        AppDbContext context,
        IOpenRouterService openRouterService,
        IEmailSender email,
        IAlertCooldownCache cooldown,
        ILogger<SpendAlertJob> logger)
    {
        _context = context;
        _openRouterService = openRouterService;
        _email = email;
        _cooldown = cooldown;
        _logger = logger;
    }

    private static decimal Threshold(string name, decimal fallback) =>
        decimal.TryParse(Environment.GetEnvironmentVariable(name), out var v) && v > 0 ? v : fallback;

    private static int IntThreshold(string name, int fallback) =>
        int.TryParse(Environment.GetEnvironmentVariable(name), out var v) && v > 0 ? v : fallback;

    private static bool Enabled =>
        !bool.TryParse(Environment.GetEnvironmentVariable("SPEND_ALERTS_ENABLED"), out var e) || e;

    public async Task CheckAsync()
    {
        if (!Enabled)
        {
            _logger.LogDebug("Spend alerts disabled");
            return;
        }

        if (!_email.IsConfigured)
        {
            _logger.LogWarning("Spend alert check skipped: SMTP is not configured");
            return;
        }

        try
        {
            var alerts = await EvaluateAsync();

            // Drop anything already reported inside its cooldown window.
            var toSend = alerts.Where(a => !_cooldown.IsSuppressed(a.Key)).ToList();

            if (toSend.Count == 0)
            {
                if (alerts.Count > 0)
                    _logger.LogDebug("{Count} alert(s) still firing but suppressed by cooldown", alerts.Count);
                return;
            }

            _logger.LogWarning("Sending {Count} spend alert(s): {Keys}",
                toSend.Count, string.Join(", ", toSend.Select(a => a.Key)));

            var sent = await _email.SendAsync(
                EmailTemplates.AlertSubject(toSend),
                EmailTemplates.AlertHtml(toSend),
                EmailTemplates.AlertText(toSend));

            // Only start the cooldown once the mail actually left, so a transient
            // SMTP failure does not silence a real alert for hours.
            if (sent)
            {
                foreach (var alert in toSend)
                    _cooldown.MarkSent(alert.Key);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Spend alert check failed");
        }
    }

    public async Task<List<SpendAlert>> EvaluateAsync(CancellationToken cancellationToken = default)
    {
        var alerts = new List<SpendAlert>();
        var since = DateTime.UtcNow.AddHours(-24);
        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var dayLogs = await _context.UsageLogs
            .Where(u => u.CreatedAt >= since)
            .Select(u => new { u.ProjectId, u.ModelId, u.CostUsd })
            .ToListAsync(cancellationToken);

        // 1. Total spend across the gateway in the last 24h.
        var globalDaily = dayLogs.Sum(l => l.CostUsd);
        var globalLimit = Threshold("ALERT_GLOBAL_DAILY_USD", 5m);
        if (globalDaily > globalLimit)
        {
            alerts.Add(new SpendAlert
            {
                Key = "global-daily-spend",
                Severity = AlertSeverity.Critical,
                Title = "Gateway spend is unusually high",
                Detail = $"{Fmt(globalDaily)} spent in the last 24 hours, over the {Fmt(globalLimit)} threshold, across {dayLogs.Count:N0} requests.",
                Action = "Check which project is responsible below, and pause it from the Projects page if you do not recognise the traffic.",
                Observed = globalDaily,
                Threshold = globalLimit
            });
        }

        // 2. Per-project spend. The first place a leaked key shows up.
        var projectLimit = Threshold("ALERT_PROJECT_DAILY_USD", 2m);
        var projectNames = await _context.Projects
            .Select(p => new { p.Id, p.Name })
            .ToListAsync(cancellationToken);
        var nameById = projectNames.ToDictionary(p => p.Id, p => p.Name);

        foreach (var group in dayLogs.GroupBy(l => l.ProjectId))
        {
            var spend = group.Sum(l => l.CostUsd);
            if (spend <= projectLimit) continue;

            var name = nameById.TryGetValue(group.Key, out var n) ? n : group.Key.ToString();
            alerts.Add(new SpendAlert
            {
                Key = $"project-daily-spend:{group.Key}",
                Severity = AlertSeverity.Critical,
                Title = $"Project \"{name}\" is spending fast",
                Detail = $"{Fmt(spend)} in the last 24 hours across {group.Count():N0} requests, over the {Fmt(projectLimit)} per-project threshold.",
                Action = "If this is not expected traffic, rotate that project's API key and pause the project.",
                Observed = spend,
                Threshold = projectLimit
            });
        }

        // 3. Request-count spike. Catches a leaked key even on free models, where
        //    spend stays at zero and the spend rules never fire.
        var requestLimit = IntThreshold("ALERT_PROJECT_DAILY_REQUESTS", 5000);
        foreach (var group in dayLogs.GroupBy(l => l.ProjectId))
        {
            var count = group.Count();
            if (count <= requestLimit) continue;

            var name = nameById.TryGetValue(group.Key, out var n) ? n : group.Key.ToString();
            alerts.Add(new SpendAlert
            {
                Key = $"project-daily-requests:{group.Key}",
                Severity = AlertSeverity.Warning,
                Title = $"Project \"{name}\" is making a lot of requests",
                Detail = $"{count:N0} requests in the last 24 hours, over the {requestLimit:N0} threshold. Spend so far is {Fmt(group.Sum(l => l.CostUsd))}.",
                Action = "Free models cost nothing but still signal a leaked key. Rotate the key if the volume is not yours.",
                Observed = count,
                Threshold = requestLimit
            });
        }

        // 4. A single model eating the budget.
        var modelLimit = Threshold("ALERT_MODEL_DAILY_USD", 3m);
        foreach (var group in dayLogs.GroupBy(l => l.ModelId))
        {
            var spend = group.Sum(l => l.CostUsd);
            if (spend <= modelLimit) continue;

            alerts.Add(new SpendAlert
            {
                Key = $"model-daily-spend:{group.Key}",
                Severity = AlertSeverity.Warning,
                Title = $"One model is dominating spend",
                Detail = $"{group.Key} cost {Fmt(spend)} in the last 24 hours across {group.Count():N0} requests, over the {Fmt(modelLimit)} threshold.",
                Action = "Consider selecting a cheaper model for that lane, or capping max_tokens on premium requests.",
                Observed = spend,
                Threshold = modelLimit
            });
        }

        // 5. Month-to-date budget.
        var monthlyLimit = Threshold("ALERT_MONTHLY_USD", 25m);
        var monthSpend = await _context.UsageLogs
            .Where(u => u.CreatedAt >= monthStart)
            .SumAsync(u => (decimal?)u.CostUsd, cancellationToken) ?? 0m;
        if (monthSpend > monthlyLimit)
        {
            alerts.Add(new SpendAlert
            {
                Key = $"monthly-budget:{monthStart:yyyy-MM}",
                Severity = AlertSeverity.Warning,
                Title = "Monthly budget passed",
                Detail = $"{Fmt(monthSpend)} spent so far this month, over the {Fmt(monthlyLimit)} budget.",
                Action = "Raise ALERT_MONTHLY_USD if this is the new normal, or move traffic to a cheaper lane.",
                Observed = monthSpend,
                Threshold = monthlyLimit
            });
        }

        // 6. OpenRouter credit and key health.
        await AddCreditAlertsAsync(alerts, cancellationToken);

        return alerts;
    }

    private async Task AddCreditAlertsAsync(List<SpendAlert> alerts, CancellationToken cancellationToken)
    {
        OpenRouterCredit? credit;
        try
        {
            credit = await _openRouterService.GetCreditsAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read OpenRouter credit during alert check");
            return;
        }

        if (credit is null) return;

        var creditFloor = Threshold("ALERT_OPENROUTER_CREDIT_USD", 2m);
        if (credit.Remaining < creditFloor)
        {
            alerts.Add(new SpendAlert
            {
                Key = "openrouter-credit-low",
                Severity = credit.Remaining <= 0 ? AlertSeverity.Critical : AlertSeverity.Warning,
                Title = credit.Remaining <= 0 ? "OpenRouter credit is exhausted" : "OpenRouter credit is running low",
                Detail = $"{Fmt(credit.Remaining)} left of {Fmt(credit.TotalCredits)}. Paid requests start failing at zero; free lanes keep working.",
                Action = "Top up at openrouter.ai/credits.",
                Observed = credit.Remaining,
                Threshold = creditFloor
            });
        }

        if (credit.KeyLimit.HasValue && credit.KeyLimitRemaining.HasValue)
        {
            var keyFloor = Threshold("ALERT_OPENROUTER_KEY_REMAINING_USD", 1m);
            if (credit.KeyLimitRemaining.Value < keyFloor)
            {
                alerts.Add(new SpendAlert
                {
                    Key = "openrouter-key-limit-low",
                    Severity = AlertSeverity.Warning,
                    Title = "OpenRouter key limit nearly reached",
                    Detail = $"{Fmt(credit.KeyLimitRemaining.Value)} left of this key's {Fmt(credit.KeyLimit.Value)} cap.",
                    Action = "Raise the key's limit in the OpenRouter dashboard, or issue a new key.",
                    Observed = credit.KeyLimitRemaining.Value,
                    Threshold = keyFloor
                });
            }
        }

        if (credit.KeyExpiresAt.HasValue)
        {
            var days = IntThreshold("ALERT_OPENROUTER_KEY_EXPIRY_DAYS", 14);
            var daysLeft = (credit.KeyExpiresAt.Value - DateTime.UtcNow).TotalDays;
            if (daysLeft <= days)
            {
                alerts.Add(new SpendAlert
                {
                    Key = "openrouter-key-expiring",
                    Severity = daysLeft <= 0 ? AlertSeverity.Critical : AlertSeverity.Warning,
                    Title = daysLeft <= 0 ? "OpenRouter key has expired" : "OpenRouter key expires soon",
                    Detail = daysLeft <= 0
                        ? $"The key expired on {credit.KeyExpiresAt.Value:d MMM yyyy}. Every paid request is failing."
                        : $"The key expires on {credit.KeyExpiresAt.Value:d MMM yyyy}, in {daysLeft:0} days.",
                    Action = "Issue a replacement key and update OPENROUTER_API_KEY in the production .env, then restart.",
                });
            }
        }
    }

    private static string Fmt(decimal value) =>
        value < 0.01m && value > 0 ? $"${value:0.000000}" : $"${value:N2}";
}

using System.Collections.Concurrent;
using Freeway.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Freeway.Infrastructure.Services;

/// <summary>
/// Keeps a fired alert quiet for a while so a single runaway key produces one
/// email rather than one per check.
///
/// State is in memory, matching the other caches in this project. A restart can
/// therefore repeat an alert once, which is the right way round: a duplicate
/// warning is cheap, a missed one is not.
/// </summary>
public class AlertCooldownCache : IAlertCooldownCache
{
    private readonly ILogger<AlertCooldownCache> _logger;
    private readonly ConcurrentDictionary<string, DateTime> _sentAt = new();
    private readonly TimeSpan _cooldown;

    public AlertCooldownCache(ILogger<AlertCooldownCache> logger)
    {
        _logger = logger;

        var hours = int.TryParse(Environment.GetEnvironmentVariable("ALERT_COOLDOWN_HOURS"), out var h) && h > 0
            ? h
            : 12;
        _cooldown = TimeSpan.FromHours(hours);
    }

    public bool IsSuppressed(string alertKey)
    {
        if (!_sentAt.TryGetValue(alertKey, out var sent))
            return false;

        if (DateTime.UtcNow - sent < _cooldown)
            return true;

        _sentAt.TryRemove(alertKey, out _);
        return false;
    }

    public void MarkSent(string alertKey)
    {
        _sentAt[alertKey] = DateTime.UtcNow;
        _logger.LogDebug("Alert {Key} suppressed for {Hours}h", alertKey, _cooldown.TotalHours);
    }
}

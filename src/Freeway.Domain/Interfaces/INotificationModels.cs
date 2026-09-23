namespace Freeway.Domain.Interfaces;

/// <summary>
/// Sends mail on behalf of the gateway. One implementation (SMTP); kept behind an
/// interface so a job can be tested without a mail server.
/// </summary>
public interface IEmailSender
{
    bool IsConfigured { get; }

    /// <summary>Recipient the gateway reports to.</summary>
    string AdminEmail { get; }

    Task<bool> SendAsync(
        string subject,
        string htmlBody,
        string textBody,
        CancellationToken cancellationToken = default);
}

/// <summary>Suppresses repeat alerts so one runaway key does not fill an inbox.</summary>
public interface IAlertCooldownCache
{
    /// <summary>True when this alert was already sent inside its cooldown window.</summary>
    bool IsSuppressed(string alertKey);

    void MarkSent(string alertKey);
}

// ---------------------------------------------------------------------------
// Weekly report
// ---------------------------------------------------------------------------

public class UsageTotals
{
    public int Requests { get; set; }
    public int FailedRequests { get; set; }
    public long InputTokens { get; set; }
    public long OutputTokens { get; set; }
    public decimal CostUsd { get; set; }
    public double AvgResponseTimeMs { get; set; }

    public long TotalTokens => InputTokens + OutputTokens;
    public double SuccessRate => Requests == 0 ? 100 : (double)(Requests - FailedRequests) / Requests * 100;
}

public class ProjectUsageLine
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public int Requests { get; set; }
    public long Tokens { get; set; }
    public decimal CostUsd { get; set; }
    public int FailedRequests { get; set; }
}

public class ModelUsageLine
{
    public string ModelId { get; set; } = string.Empty;
    public string ModelType { get; set; } = string.Empty;
    public string? ModelTier { get; set; }
    public int Requests { get; set; }
    public long Tokens { get; set; }
    public decimal CostUsd { get; set; }

    /// <summary>Average spend per request — what makes a model expensive, not its total.</summary>
    public decimal CostPerRequest => Requests == 0 ? 0 : CostUsd / Requests;
}

public class TierUsageLine
{
    /// <summary>"free", "low", "moderate", "premium", "image" or "other".</summary>
    public string Tier { get; set; } = string.Empty;
    public int Requests { get; set; }
    public decimal CostUsd { get; set; }
}

public class OpenRouterCredit
{
    public decimal TotalCredits { get; set; }
    public decimal TotalUsage { get; set; }
    public decimal Remaining => TotalCredits - TotalUsage;

    /// <summary>Per-key spend cap, when the key has one.</summary>
    public decimal? KeyLimit { get; set; }
    public decimal? KeyLimitRemaining { get; set; }
    public DateTime? KeyExpiresAt { get; set; }
}

public class WeeklyUsageReport
{
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }

    public UsageTotals ThisWeek { get; set; } = new();
    public UsageTotals PreviousWeek { get; set; } = new();
    public UsageTotals MonthToDate { get; set; } = new();
    public UsageTotals AllTime { get; set; } = new();

    public List<ProjectUsageLine> Projects { get; set; } = new();
    public List<ModelUsageLine> Models { get; set; } = new();
    public List<TierUsageLine> Tiers { get; set; } = new();

    public int TotalProjects { get; set; }
    public int ActiveProjects { get; set; }

    public OpenRouterCredit? Credit { get; set; }

    public bool HadTraffic => ThisWeek.Requests > 0;

    /// <summary>Week-over-week cost change. Null when there is no prior week to compare.</summary>
    public double? CostChangePercent =>
        PreviousWeek.CostUsd == 0
            ? null
            : (double)((ThisWeek.CostUsd - PreviousWeek.CostUsd) / PreviousWeek.CostUsd * 100);

    public ModelUsageLine? MostUsedModel => Models.OrderByDescending(m => m.Requests).FirstOrDefault();
    public ModelUsageLine? MostExpensiveModel => Models.OrderByDescending(m => m.CostUsd).FirstOrDefault();

    /// <summary>Cheapest per request among models that actually cost something.</summary>
    public ModelUsageLine? BestValueModel =>
        Models.Where(m => m.CostUsd > 0).OrderBy(m => m.CostPerRequest).FirstOrDefault();

    public ProjectUsageLine? TopProject => Projects.OrderByDescending(p => p.CostUsd).FirstOrDefault();
}

// ---------------------------------------------------------------------------
// Alerts
// ---------------------------------------------------------------------------

public enum AlertSeverity
{
    Info,
    Warning,
    Critical
}

public class SpendAlert
{
    /// <summary>Stable identity for cooldown suppression, e.g. "project-daily-spend:{id}".</summary>
    public string Key { get; set; } = string.Empty;

    public AlertSeverity Severity { get; set; } = AlertSeverity.Warning;
    public string Title { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;

    /// <summary>What the operator should do about it.</summary>
    public string? Action { get; set; }

    public decimal? Observed { get; set; }
    public decimal? Threshold { get; set; }
}

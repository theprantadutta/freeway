namespace Freeway.Application.DTOs;

/// <summary>Everything the dashboard's Credits page shows, in one response.</summary>
public class CreditOverviewDto
{
    public string Provider { get; set; } = "anthropic";
    public bool Configured { get; set; }
    public bool CanServe { get; set; }

    /// <summary>"off", "unchecked", "active", "no_grant", "exhausted" or "blocked".</summary>
    public string State { get; set; } = "off";
    public string Summary { get; set; } = string.Empty;
    public string? BlockedReason { get; set; }
    public DateTime? BlockedUntil { get; set; }
    public string? LastError { get; set; }
    public DateTime? LastErrorAt { get; set; }
    public DateTime? RefreshedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }

    /// <summary>Lanes served from the credit, and the model each uses.</summary>
    public List<CreditLaneDto> Lanes { get; set; } = new();

    public decimal StopAtPercent { get; set; }

    public CreditGrantDto? Grant { get; set; }

    /// <summary>The API key in use, as far as Freeway can know it. Null until first seen.</summary>
    public CreditKeyDto? Key { get; set; }

    /// <summary>Spent this cycle at list price, from Freeway's own count.</summary>
    public decimal UsedUsd { get; set; }
    public decimal RemainingUsd { get; set; }

    /// <summary>What Freeway will spend before stopping: the grant times the stop-at share.</summary>
    public decimal UsableUsd { get; set; }
    public decimal UsedPercent { get; set; }

    public double DaysTotal { get; set; }
    public double DaysElapsed { get; set; }
    public double DaysLeft { get; set; }

    /// <summary>Average spend per day so far this cycle.</summary>
    public decimal BurnPerDayUsd { get; set; }

    /// <summary>Where the cycle ends up at the current rate, capped at the grant.</summary>
    public decimal ProjectedUsd { get; set; }

    /// <summary>Credit that will expire unused at the current rate.</summary>
    public decimal ProjectedUnusedUsd { get; set; }

    public int Requests { get; set; }
    public int FailedRequests { get; set; }
    public long InputTokens { get; set; }
    public long OutputTokens { get; set; }

    /// <summary>What the same requests would have cost on the paid models they displaced.</summary>
    public decimal AvoidedUsd { get; set; }

    public List<CreditDayDto> Series { get; set; } = new();
    public List<CreditBreakdownDto> ByProject { get; set; } = new();
    public List<CreditBreakdownDto> ByModel { get; set; } = new();
    public List<CreditRequestDto> Recent { get; set; } = new();
    public List<CreditGrantDto> History { get; set; } = new();
}

public class CreditLaneDto
{
    public string Tier { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
}

public class CreditGrantDto
{
    public Guid Id { get; set; }
    public decimal AmountUsd { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string Source { get; set; } = "auto";
    public string? Note { get; set; }

    /// <summary>Spent against this grant. Filled in for the history list.</summary>
    public decimal UsedUsd { get; set; }
    public int Requests { get; set; }
}

public class CreditKeyDto
{
    public string Hint { get; set; } = string.Empty;
    public string Fingerprint { get; set; } = string.Empty;
    public DateTime FirstSeenAt { get; set; }
    public DateTime LastSeenAt { get; set; }
    public DateTime? ExpiresAt { get; set; }

    /// <summary>"estimated" from the configured lifetime, or "manual" once set on the dashboard.</summary>
    public string ExpirySource { get; set; } = "estimated";
    public double? DaysLeft { get; set; }
    public DateTime? LastRejectedAt { get; set; }
    public string? LastRejection { get; set; }
    public DateTime? WarnedAt { get; set; }
    public DateTime? ExpiredNoticeAt { get; set; }
}

public class UpdateCreditKeyRequest
{
    /// <summary>When the key expires, as shown on the Console's API keys page. Null for never.</summary>
    public DateTime? ExpiresAt { get; set; }
}

public class CreditDayDto
{
    public string Date { get; set; } = string.Empty;
    public decimal CreditUsd { get; set; }
    public int Requests { get; set; }
    public long Tokens { get; set; }
}

public class CreditBreakdownDto
{
    /// <summary>Project id or model id.</summary>
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Tier { get; set; }
    public int Requests { get; set; }
    public decimal CreditUsd { get; set; }
    public long Tokens { get; set; }
}

public class CreditRequestDto
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public string? ModelTier { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public decimal CreditUsd { get; set; }
    public decimal? AvoidedUsd { get; set; }
    public int ResponseTimeMs { get; set; }
    public bool Streamed { get; set; }
    public bool Success { get; set; }
}

public class UpdateCreditGrantRequest
{
    public decimal? AmountUsd { get; set; }
    public DateTime? StartsAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? Note { get; set; }
}

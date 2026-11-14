namespace Freeway.Application.DTOs;

/// <summary>
/// Everything the dashboard needs, in one call.
///
/// The panel previously issued six requests to assemble a page that still could not
/// answer "what happened over time", because no endpoint exposed a time series. This
/// carries the whole board: totals, a daily series, the lane split, and the leaders.
/// </summary>
public class OverviewDto
{
    public OverviewTotalsDto Totals { get; set; } = new();

    /// <summary>One point per day, oldest first, with empty days filled in.</summary>
    public List<UsagePointDto> Series { get; set; } = new();

    public List<LaneUsageDto> Lanes { get; set; } = new();
    public List<TopModelDto> TopModels { get; set; } = new();
    public List<TopProjectDto> TopProjects { get; set; } = new();
    public List<RecentRequestDto> Recent { get; set; } = new();

    /// <summary>Days covered by <see cref="Series"/>.</summary>
    public int Days { get; set; }

    /// <summary>The project the figures are limited to, or null for the whole gateway.</summary>
    public Guid? ProjectId { get; set; }

    /// <summary>The selected window (the <see cref="Days"/> covered by the series), totalled.</summary>
    public WindowTotalsDto Window { get; set; } = new();

    /// <summary>The same number of days immediately before the window, for comparison.</summary>
    public WindowTotalsDto PreviousWindow { get; set; } = new();

    /// <summary>
    /// Who actually answered the window's requests: the Claude API credit, the Claude
    /// Code bridge, a free provider, or OpenRouter. Only the last is billed.
    /// </summary>
    public List<BackendUsageDto> Backends { get; set; } = new();

    /// <summary>
    /// Requests by project, lane and backend over the window: how traffic moved from
    /// the apps that sent it, through the lane each asked for, to whatever answered.
    /// </summary>
    public List<FlowDto> Flows { get; set; } = new();
}

/// <summary>Totals over a span of days.</summary>
public class WindowTotalsDto
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public int Requests { get; set; }
    public int Failures { get; set; }
    public long Tokens { get; set; }

    /// <summary>Money actually billed.</summary>
    public decimal Cost { get; set; }

    /// <summary>Taken off the Claude API credit.</summary>
    public decimal CreditUsd { get; set; }

    /// <summary>What the requests served for free would have cost on a paid model.</summary>
    public decimal AvoidedUsd { get; set; }

    /// <summary>Successful requests answered by anything other than a billed provider.</summary>
    public int Unbilled { get; set; }

    public int Streamed { get; set; }
    public double AvgResponseMs { get; set; }
    public double P50ResponseMs { get; set; }
    public double P95ResponseMs { get; set; }
}

public class BackendUsageDto
{
    /// <summary>credit / bridge / free / openrouter / other.</summary>
    public string Backend { get; set; } = string.Empty;
    public int Requests { get; set; }
    public int Failures { get; set; }
    public long Tokens { get; set; }
    public decimal Cost { get; set; }
    public decimal CreditUsd { get; set; }
    public decimal AvoidedUsd { get; set; }
    public double AvgResponseMs { get; set; }
}

public class FlowDto
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string Lane { get; set; } = string.Empty;
    public string Backend { get; set; } = string.Empty;
    public int Requests { get; set; }
    public long Tokens { get; set; }
    public decimal Cost { get; set; }
}

public class OverviewTotalsDto
{
    public int TotalProjects { get; set; }
    public int ActiveProjects { get; set; }

    public int RequestsToday { get; set; }
    public int RequestsThisMonth { get; set; }
    public int RequestsAllTime { get; set; }

    public decimal CostToday { get; set; }
    public decimal CostThisMonth { get; set; }
    public decimal CostAllTime { get; set; }

    /// <summary>Same calendar span as this month, one month earlier, for comparison.</summary>
    public decimal CostPreviousMonth { get; set; }
    public int RequestsPreviousMonth { get; set; }

    public long TokensThisMonth { get; set; }
    public int FailuresThisMonth { get; set; }
    public double SuccessRateThisMonth { get; set; }
    public double AvgResponseMsThisMonth { get; set; }

    /// <summary>Taken off the Claude API credit this month.</summary>
    public decimal CreditThisMonth { get; set; }

    /// <summary>What this month's free-served requests would have cost on a paid model.</summary>
    public decimal AvoidedThisMonth { get; set; }
}

public class UsagePointDto
{
    public DateTime Date { get; set; }
    public int Requests { get; set; }
    public int Failures { get; set; }
    public decimal Cost { get; set; }
    public long Tokens { get; set; }
    public decimal CreditUsd { get; set; }
    public decimal AvoidedUsd { get; set; }

    /// <summary>Requests per lane that day (free / low / moderate / premium / image / other).</summary>
    public Dictionary<string, int> Lanes { get; set; } = new();

    /// <summary>Requests per backend that day (credit / bridge / free / openrouter / other).</summary>
    public Dictionary<string, int> Backends { get; set; } = new();
}

public class LaneUsageDto
{
    /// <summary>free / low / moderate / premium / image / other.</summary>
    public string Lane { get; set; } = string.Empty;
    public int Requests { get; set; }
    public decimal Cost { get; set; }
    public long Tokens { get; set; }
}

public class TopModelDto
{
    public string ModelId { get; set; } = string.Empty;
    public string ModelType { get; set; } = string.Empty;
    public string? ModelTier { get; set; }
    public int Requests { get; set; }
    public decimal Cost { get; set; }
    public long Tokens { get; set; }
    public int Failures { get; set; }
    public double AvgResponseMs { get; set; }
    public decimal CreditUsd { get; set; }

    /// <summary>The backend that answered most of this model's requests.</summary>
    public string Backend { get; set; } = string.Empty;
}

public class TopProjectDto
{
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Requests { get; set; }
    public decimal Cost { get; set; }
    public bool IsActive { get; set; }
    public long Tokens { get; set; }
    public int Failures { get; set; }
    public decimal CreditUsd { get; set; }
    public double AvgResponseMs { get; set; }

    /// <summary>The lane this project sends most of its requests to.</summary>
    public string TopLane { get; set; } = string.Empty;

    /// <summary>Requests per day over the window, aligned with the overview's series.</summary>
    public List<int> Daily { get; set; } = new();
}

public class RecentRequestDto
{
    public Guid Id { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public string ModelType { get; set; } = string.Empty;
    public string? ModelTier { get; set; }
    public bool Success { get; set; }
    public int ResponseTimeMs { get; set; }
    public decimal CostUsd { get; set; }
    public int TotalTokens { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid ProjectId { get; set; }
    public string? Provider { get; set; }

    /// <summary>credit / bridge / free / openrouter / other.</summary>
    public string Backend { get; set; } = string.Empty;
    public string? CostSource { get; set; }
    public decimal? CreditCostUsd { get; set; }
    public decimal? AvoidedCostUsd { get; set; }
    public bool Streamed { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public string? ErrorMessage { get; set; }
}

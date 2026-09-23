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
}

public class UsagePointDto
{
    public DateTime Date { get; set; }
    public int Requests { get; set; }
    public int Failures { get; set; }
    public decimal Cost { get; set; }
    public long Tokens { get; set; }
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
}

public class TopProjectDto
{
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Requests { get; set; }
    public decimal Cost { get; set; }
    public bool IsActive { get; set; }
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
}

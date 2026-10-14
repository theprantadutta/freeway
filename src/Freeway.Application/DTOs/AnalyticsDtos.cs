using System.Text.Json.Serialization;
using Freeway.Application.Common;

namespace Freeway.Application.DTOs;

public class GlobalSummaryDto
{
    public int TotalProjects { get; set; }
    public int ActiveProjects { get; set; }
    public int RequestsToday { get; set; }
    public int RequestsThisMonth { get; set; }
    public decimal TotalCostToday { get; set; }
    public decimal TotalCostThisMonth { get; set; }
}

public class UsageSummaryDto
{
    public int TotalRequests { get; set; }
    public int SuccessfulRequests { get; set; }
    public int FailedRequests { get; set; }

    // long, not int: these are lifetime sums for a project, and a busy one passes
    // int.MaxValue eventually. LINQ's Sum throws on overflow rather than wrapping,
    // so the endpoint would start returning 500s instead of a wrong number.
    public long TotalInputTokens { get; set; }
    public long TotalOutputTokens { get; set; }

    public decimal TotalCostUsd { get; set; }
    public double AvgResponseTimeMs { get; set; }

    /// <summary>
    /// Percentage, 0-100. Computed here because every caller wants it and none of
    /// them should be dividing counts themselves.
    /// </summary>
    public double SuccessRate =>
        TotalRequests == 0 ? 100 : (double)SuccessfulRequests / TotalRequests * 100;

    public long TotalTokens => TotalInputTokens + TotalOutputTokens;
}

public class ModelUsageStatsDto
{
    public string ModelId { get; set; } = string.Empty;
    public string ModelType { get; set; } = string.Empty;

    /// <summary>Paid tier slug, or null for free/image traffic and for pre-tier rows.</summary>
    public string? ModelTier { get; set; }
    public int Requests { get; set; }
    public long Tokens { get; set; }
    public decimal CostUsd { get; set; }
}

public class ProjectUsageDto
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public PeriodDto Period { get; set; } = new();
    public UsageSummaryDto Summary { get; set; } = new();
    public List<ModelUsageStatsDto> ByModel { get; set; } = new();
}

public class PeriodDto
{
    public DateTime? Start { get; set; }
    public DateTime? End { get; set; }
}

public class UsageLogDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string ModelId { get; set; } = string.Empty;
    public string ModelType { get; set; } = string.Empty;

    /// <summary>Paid tier slug, or null for free/image traffic and for pre-tier rows.</summary>
    public string? ModelTier { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int ResponseTimeMs { get; set; }
    public decimal CostUsd { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? RequestId { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? Provider { get; set; }
    public List<ChatMessageDto>? RequestMessages { get; set; }
    public string? ResponseContent { get; set; }
    public string? FinishReason { get; set; }

    /// <summary>
    /// What the caller asked for, as sent. Null when they did not ask, which is how
    /// you tell "this app is not sending it" apart from "it sent the default".
    /// </summary>
    public string? ReasoningEffort { get; set; }

    public int? RequestedMaxTokens { get; set; }

    /// <summary>The caller asked for a stream.</summary>
    public bool Streamed { get; set; }

    /// <summary>For a stream, the wait before its first token. Null otherwise.</summary>
    public int? TimeToFirstTokenMs { get; set; }
}

public class ChatMessageDto
{
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Accepts either a string or an array of content parts, as OpenAI clients send
    /// both; text parts are joined. Always written back as a string.
    /// </summary>
    [JsonConverter(typeof(MessageContentJsonConverter))]
    public string Content { get; set; } = string.Empty;
}

public class UsageLogsResponseDto
{
    public List<UsageLogDto> Logs { get; set; } = new();
    public int TotalCount { get; set; }
    public int Limit { get; set; }
    public int Offset { get; set; }
}

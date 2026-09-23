using Freeway.Domain.Common;

namespace Freeway.Domain.Entities;

public class UsageLog : BaseEntity
{
    public Guid ProjectId { get; set; }
    public string ModelId { get; set; } = string.Empty;
    public string ModelType { get; set; } = string.Empty; // "free", "paid", "image", "specific"

    /// <summary>
    /// Paid tier the request was served from: "low", "moderate" or "premium".
    /// Null for free/image requests and for rows written before tiers existed.
    /// <see cref="ModelType"/> deliberately keeps its original values so existing
    /// analytics grouping and historical rows stay comparable.
    /// </summary>
    public string? ModelTier { get; set; }

    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int ResponseTimeMs { get; set; }
    public decimal CostUsd { get; set; }
    public decimal? PromptCostPerToken { get; set; }
    public decimal? CompletionCostPerToken { get; set; }
    public bool Success { get; set; } = true;
    public string? ErrorMessage { get; set; }
    public string? RequestId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? Provider { get; set; }
    public List<ChatMessage>? RequestMessages { get; set; }
    public string? ResponseContent { get; set; }
    public string? FinishReason { get; set; }
    public Dictionary<string, object>? RequestParams { get; set; }

    public Project Project { get; set; } = null!;
}

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

    /// <summary>
    /// Provenance of <see cref="CostUsd"/>: "provider" (the upstream told us the
    /// billed amount), "free_tier" (a provider's own free tier, genuinely zero),
    /// "estimated" (tokens times a cached price) or "legacy" (written before cost
    /// accounting was fixed, and under-reported). Null on rows predating this.
    /// </summary>
    public string? CostSource { get; set; }

    /// <summary>The endpoint that actually served the request behind an aggregator.</summary>
    public string? UpstreamProvider { get; set; }

    /// <summary>
    /// What the caller asked for, as opposed to what came back. Both are null when
    /// the caller said nothing.
    ///
    /// Recorded because a request's shape is otherwise invisible after the fact, and
    /// that is exactly when it is wanted: after changing how an app calls the gateway,
    /// the only question is whether the change is live. Token counts answer it slowly
    /// and ambiguously -- output falling could be a smaller cap, lower reasoning
    /// effort or simply shorter questions -- while these two say so outright.
    /// </summary>
    public string? ReasoningEffort { get; set; }

    /// <summary>The max_tokens the caller sent, not the tokens produced.</summary>
    public int? RequestedMaxTokens { get; set; }

    /// <summary>
    /// List price avoided by serving this out of a subscription rather than a paid
    /// provider. Null for anything that was actually billed.
    /// </summary>
    public decimal? AvoidedCostUsd { get; set; }
    public List<ChatMessage>? RequestMessages { get; set; }
    public string? ResponseContent { get; set; }
    public string? FinishReason { get; set; }
    public Dictionary<string, object>? RequestParams { get; set; }

    public Project Project { get; set; } = null!;
}

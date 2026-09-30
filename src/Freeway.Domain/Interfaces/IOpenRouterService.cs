using Freeway.Domain.Entities;

namespace Freeway.Domain.Interfaces;

public interface IOpenRouterService
{
    Task<List<OpenRouterModel>> GetModelsAsync(CancellationToken cancellationToken = default);
    Task<List<OpenRouterModel>> GetImageModelsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Account credit and per-key limit. Null when the call fails, so a monitoring
    /// job can tell "no data" apart from "no credit left".
    /// </summary>
    Task<OpenRouterCredit?> GetCreditsAsync(CancellationToken cancellationToken = default);
    Task<ChatCompletionResult> CreateChatCompletionAsync(
        string modelId,
        List<ChatMessage> messages,
        ChatCompletionOptions? options = null,
        CancellationToken cancellationToken = default);
}

public class OpenRouterModel
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? ContextLength { get; set; }
    public OpenRouterPricing Pricing { get; set; } = new();
    public OpenRouterArchitecture? Architecture { get; set; }
    public long? Created { get; set; }
}

public class OpenRouterPricing
{
    public string Prompt { get; set; } = "0";
    public string Completion { get; set; } = "0";
    public string? Request { get; set; }

    /// <summary>Price for an image supplied as input (vision).</summary>
    public string? Image { get; set; }

    /// <summary>
    /// Price for generated image output. This is what an image model actually
    /// charges: most of them report 0 for prompt and completion.
    /// </summary>
    public string? ImageOutput { get; set; }
}

public class OpenRouterArchitecture
{
    public string? Modality { get; set; }
    public string? Tokenizer { get; set; }
    public string? InstructType { get; set; }
}

public class ChatCompletionOptions
{
    public double? Temperature { get; set; }
    public int? MaxTokens { get; set; }
    public double? TopP { get; set; }
    public double? FrequencyPenalty { get; set; }
    public double? PresencePenalty { get; set; }
    public List<string>? Stop { get; set; }
    public bool Stream { get; set; }

    /// <summary>How hard a reasoning model should think. Ignored by models without it.</summary>
    public ReasoningOptions? Reasoning { get; set; }
}

/// <summary>
/// Controls the thinking a reasoning model does before it answers.
///
/// Worth having because reasoning is billed as output and is usually the larger
/// half of it. Measured on a short persona reply through openai/gpt-5-mini: 488
/// completion tokens by default, of which 320 were reasoning nobody reads, against
/// 246 with effort "low", of which 64 were. Same answer, half the bill.
///
/// Only "effort" reliably changes anything. "exclude" hides the reasoning from the
/// response but still pays for it, "max_tokens" was ignored, and some endpoints
/// reject enabled=false outright with "Reasoning is mandatory for this endpoint".
/// All of it is passed through as sent, so callers can use whatever a model
/// supports without waiting on this gateway to learn about it.
/// </summary>
public class ReasoningOptions
{
    /// <summary>"low", "medium" or "high".</summary>
    public string? Effort { get; set; }

    /// <summary>Omit the reasoning from the reply. Does not make it cheaper.</summary>
    public bool? Exclude { get; set; }

    public int? MaxTokens { get; set; }

    /// <summary>Some endpoints refuse false and fail the request.</summary>
    public bool? Enabled { get; set; }
}

public class ChatCompletionResult
{
    public string Id { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// What the upstream actually charged, when it tells us. Preferred over any
    /// price-times-tokens estimate: with provider routing the endpoint that served
    /// a request may not charge the model's headline rate.
    /// </summary>
    public decimal? CostUsd { get; set; }

    /// <summary>Who really served it, e.g. "DeepInfra" behind OpenRouter.</summary>
    public string? UpstreamProvider { get; set; }

    /// <summary>
    /// What this request would have cost had it gone to a paid provider, for work
    /// served out of a subscription instead. Money not spent, not money spent.
    /// </summary>
    public decimal? AvoidedCostUsd { get; set; }

    /// <summary>
    /// How CostUsd was arrived at: "provider" (billed amount), "free_tier"
    /// (a provider's own free tier, genuinely zero), or "estimated".
    /// </summary>
    public string? CostSource { get; set; }
    public List<ChatCompletionChoice> Choices { get; set; } = new();
    public ChatCompletionUsage Usage { get; set; } = new();
    public long Created { get; set; }
    public string? FinishReason { get; set; }
    public bool Success { get; set; } = true;
    public string? ErrorMessage { get; set; }
    public int ResponseTimeMs { get; set; }
    public int? HttpStatusCode { get; set; }
    public string? ProviderName { get; set; }
}

public class ChatCompletionChoice
{
    public int Index { get; set; }
    public ChatMessage Message { get; set; } = new();
    public string? FinishReason { get; set; }
}

public class ChatCompletionUsage
{
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
}

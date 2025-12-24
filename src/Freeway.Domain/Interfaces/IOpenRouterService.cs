using Freeway.Domain.Entities;

namespace Freeway.Domain.Interfaces;

public interface IOpenRouterService
{
    Task<List<OpenRouterModel>> GetModelsAsync(CancellationToken cancellationToken = default);
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
    public DateTime? Created { get; set; }
}

public class OpenRouterPricing
{
    public string Prompt { get; set; } = "0";
    public string Completion { get; set; } = "0";
    public string? Request { get; set; }
    public string? Image { get; set; }
}

public class OpenRouterArchitecture
{
    public string? Modality { get; set; }
    public string? Tokenizer { get; set; }
    public int? InstructType { get; set; }
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
}

public class ChatCompletionResult
{
    public string Id { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public List<ChatCompletionChoice> Choices { get; set; } = new();
    public ChatCompletionUsage Usage { get; set; } = new();
    public long Created { get; set; }
    public string? FinishReason { get; set; }
    public bool Success { get; set; } = true;
    public string? ErrorMessage { get; set; }
    public int ResponseTimeMs { get; set; }
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

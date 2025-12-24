using Freeway.Domain.Entities;

namespace Freeway.Domain.Interfaces;

public interface IAiProvider
{
    /// <summary>
    /// Unique provider name (e.g., "gemini", "groq", "cohere", "huggingface", "mistral", "openrouter")
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Display name for logging and UI
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Whether this provider is enabled (has API key configured)
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Whether this provider offers free models (used for fallback ordering)
    /// </summary>
    bool IsFreeProvider { get; }

    /// <summary>
    /// The default model ID to use for this provider
    /// </summary>
    string DefaultModelId { get; }

    /// <summary>
    /// Create a chat completion using this provider
    /// </summary>
    Task<ChatCompletionResult> CreateChatCompletionAsync(
        string modelId,
        List<ChatMessage> messages,
        ChatCompletionOptions? options = null,
        CancellationToken cancellationToken = default);
}

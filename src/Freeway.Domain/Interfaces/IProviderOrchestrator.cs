using Freeway.Domain.Entities;

namespace Freeway.Domain.Interfaces;

public interface IProviderOrchestrator
{
    /// <summary>
    /// Execute a chat completion with automatic fallback through providers.
    /// Tries free providers first (ranked by benchmark), then falls back to OpenRouter paid.
    /// </summary>
    Task<ChatCompletionResult> ExecuteWithFallbackAsync(
        List<ChatMessage> messages,
        ChatCompletionOptions? options = null,
        CancellationToken cancellationToken = default);
}

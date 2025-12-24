using Freeway.Domain.Entities;
using Freeway.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Freeway.Infrastructure.Services;

public class ProviderOrchestrator : IProviderOrchestrator
{
    private readonly IEnumerable<IAiProvider> _providers;
    private readonly IProviderBenchmarkCache _benchmarkCache;
    private readonly ILogger<ProviderOrchestrator> _logger;

    // Retry configuration
    private static readonly int[] RetryDelaysMs = [500, 1000];
    private const int MaxRetries = 2;

    public ProviderOrchestrator(
        IEnumerable<IAiProvider> providers,
        IProviderBenchmarkCache benchmarkCache,
        ILogger<ProviderOrchestrator> logger)
    {
        _providers = providers;
        _benchmarkCache = benchmarkCache;
        _logger = logger;
    }

    public async Task<ChatCompletionResult> ExecuteWithFallbackAsync(
        List<ChatMessage> messages,
        ChatCompletionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var providersDict = _providers.ToDictionary(p => p.Name, p => p);
        var rankedProviders = _benchmarkCache.GetRankedProviders();
        var errors = new List<string>();

        // Try free providers first (in ranked order)
        foreach (var providerName in rankedProviders)
        {
            if (!providersDict.TryGetValue(providerName, out var provider))
                continue;

            if (!provider.IsEnabled)
            {
                _logger.LogDebug("Skipping {Provider}: not configured", providerName);
                continue;
            }

            if (!provider.IsFreeProvider)
                continue; // Skip paid providers in this loop

            var result = await TryProviderWithRetryAsync(provider, messages, options, cancellationToken);

            if (result.Success)
            {
                _logger.LogInformation("Request succeeded with {Provider}", providerName);
                _benchmarkCache.AddBenchmarkResult(providerName, result.ResponseTimeMs, true);
                return result;
            }

            errors.Add($"{provider.DisplayName}: {result.ErrorMessage}");
            _benchmarkCache.AddBenchmarkResult(providerName, result.ResponseTimeMs, false);
        }

        // All free providers failed, try OpenRouter as paid fallback
        if (providersDict.TryGetValue("openrouter", out var openRouterProvider) && openRouterProvider.IsEnabled)
        {
            _logger.LogWarning("All free providers failed, falling back to OpenRouter paid");

            var result = await TryProviderWithRetryAsync(openRouterProvider, messages, options, cancellationToken);

            if (result.Success)
            {
                _logger.LogInformation("Request succeeded with OpenRouter (paid fallback)");
                return result;
            }

            errors.Add($"{openRouterProvider.DisplayName}: {result.ErrorMessage}");
        }

        // All providers failed
        _logger.LogError("All providers failed. Errors: {Errors}", string.Join("; ", errors));

        return new ChatCompletionResult
        {
            Success = false,
            ErrorMessage = $"All providers failed: {string.Join("; ", errors)}",
            HttpStatusCode = 502,
            ProviderName = "orchestrator"
        };
    }

    private async Task<ChatCompletionResult> TryProviderWithRetryAsync(
        IAiProvider provider,
        List<ChatMessage> messages,
        ChatCompletionOptions? options,
        CancellationToken cancellationToken)
    {
        ChatCompletionResult? lastResult = null;

        for (int attempt = 0; attempt <= MaxRetries; attempt++)
        {
            if (attempt > 0)
            {
                var delay = RetryDelaysMs[attempt - 1];
                _logger.LogDebug("Retry {Attempt}/{Max} for {Provider} after {Delay}ms",
                    attempt, MaxRetries, provider.Name, delay);
                await Task.Delay(delay, cancellationToken);
            }

            lastResult = await provider.CreateChatCompletionAsync(
                provider.DefaultModelId,
                messages,
                options,
                cancellationToken);

            if (lastResult.Success)
                return lastResult;

            // Check for rate limit (429) - immediately skip to next provider, no retry
            if (lastResult.HttpStatusCode == 429)
            {
                _logger.LogWarning("{Provider} rate limited (429), skipping to next provider", provider.Name);
                return lastResult;
            }

            // Check for client errors (4xx except 429) - skip to next provider, no retry
            if (lastResult.HttpStatusCode >= 400 && lastResult.HttpStatusCode < 500)
            {
                _logger.LogWarning("{Provider} client error ({StatusCode}), skipping to next provider",
                    provider.Name, lastResult.HttpStatusCode);
                return lastResult;
            }

            // Server errors (5xx) or timeouts - retry
            _logger.LogWarning("{Provider} error on attempt {Attempt}: {Error}",
                provider.Name, attempt + 1, lastResult.ErrorMessage);
        }

        return lastResult ?? new ChatCompletionResult
        {
            Success = false,
            ErrorMessage = "Unknown error",
            ProviderName = provider.Name
        };
    }
}

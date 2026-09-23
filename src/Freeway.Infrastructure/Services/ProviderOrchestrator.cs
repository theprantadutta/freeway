using System.Globalization;
using Freeway.Domain.Entities;
using Freeway.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Freeway.Infrastructure.Services;

public class ProviderOrchestrator : IProviderOrchestrator
{
    private readonly IEnumerable<IAiProvider> _providers;
    private readonly IProviderBenchmarkCache _benchmarkCache;
    private readonly IProviderModelCache _providerModelCache;
    private readonly IModelCacheService _modelCacheService;
    private readonly ILogger<ProviderOrchestrator> _logger;

    // Retry configuration
    private static readonly int[] RetryDelaysMs = [500, 1000];
    private const int MaxRetries = 2;

    public ProviderOrchestrator(
        IEnumerable<IAiProvider> providers,
        IProviderBenchmarkCache benchmarkCache,
        IProviderModelCache providerModelCache,
        IModelCacheService modelCacheService,
        ILogger<ProviderOrchestrator> logger)
    {
        _providers = providers;
        _benchmarkCache = benchmarkCache;
        _providerModelCache = providerModelCache;
        _modelCacheService = modelCacheService;
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

            // Get best model from cache, fallback to provider default if cache empty
            var modelId = _providerModelCache.GetBestModelId(providerName);
            if (string.IsNullOrEmpty(modelId))
            {
                _logger.LogWarning("No models cached for {Provider}, skipping", providerName);
                continue;
            }

            _logger.LogDebug("Using model {Model} for {Provider}", modelId, providerName);
            var result = await TryProviderWithRetryAsync(provider, modelId, messages, options, cancellationToken);

            if (result.Success)
            {
                _logger.LogInformation("Request succeeded with {Provider} using model {Model}", providerName, modelId);
                _benchmarkCache.AddBenchmarkResult(providerName, result.ResponseTimeMs, true);

                // These are the providers' own free tiers, billed in quota rather than
                // money. Recording that explicitly keeps a real zero distinguishable
                // from a cost we simply never worked out.
                result.CostUsd = 0m;
                result.CostSource = "free_tier";
                return result;
            }

            errors.Add($"{provider.DisplayName}: {result.ErrorMessage}");
            _benchmarkCache.AddBenchmarkResult(providerName, result.ResponseTimeMs, false);
        }

        // Last free rung: OpenRouter's own zero-cost models. These are fetched and
        // ranked on every refresh but were never actually reachable, because the loop
        // above only considers providers flagged IsFreeProvider and OpenRouter is not
        // one. That left the "selected free model" shown on the dashboard purely
        // decorative, and threw away a couple of dozen usable free models.
        if (providersDict.TryGetValue("openrouter", out var openRouter) && openRouter.IsEnabled)
        {
            foreach (var candidate in BuildOpenRouterFreeCandidates())
            {
                _logger.LogDebug("Trying OpenRouter free model {Model}", candidate.Id);
                var result = await TryProviderWithRetryAsync(
                    openRouter, candidate.Id, messages, options, cancellationToken);

                if (result.Success)
                {
                    _logger.LogInformation(
                        "Request succeeded with OpenRouter free model {Model}", candidate.Id);

                    // If OpenRouter reported what it billed, keep that: it should be
                    // zero here, and if it ever is not, that must be visible rather
                    // than overwritten with an assumption.
                    if (result.CostSource is null)
                    {
                        result.CostUsd = 0m;
                        result.CostSource = "free_tier";
                    }
                    return result;
                }

                errors.Add($"openrouter/{candidate.Id}: {result.ErrorMessage}");
            }
        }

        // Deliberately no paid fallback here.
        //
        // This used to fall through to a paid OpenRouter model when every free
        // provider failed. The request succeeded, but it was still logged as "free"
        // with a cost of zero, so real spend became invisible. A lane called "free"
        // must never produce a bill; callers that want a paid model ask for one with
        // "paid" or "paid:<tier>".
        _logger.LogError("All free providers failed. Errors: {Errors}", string.Join("; ", errors));

        return new ChatCompletionResult
        {
            Success = false,
            ErrorMessage =
                $"No free provider could serve this request: {string.Join("; ", errors)}. " +
                "Retry shortly, or request \"paid:low\" to use a paid model.",
            HttpStatusCode = 503,
            ProviderName = "orchestrator",
            CostUsd = 0m,
            CostSource = "free_tier"
        };
    }

    /// <summary>
    /// The zero-cost OpenRouter models to try, admin-selected one first then the rest
    /// by rank. Every candidate is re-checked for being genuinely free: this rung must
    /// never be able to produce a charge, whatever the catalog says.
    /// </summary>
    private List<CachedModel> BuildOpenRouterFreeCandidates()
    {
        var limit = int.TryParse(
            Environment.GetEnvironmentVariable("FREE_LANE_OPENROUTER_COUNT"), out var n) && n >= 0
            ? n
            : 3;

        if (limit == 0) return new List<CachedModel>();

        var candidates = new List<CachedModel>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(CachedModel? model)
        {
            if (model is null || string.IsNullOrEmpty(model.Id)) return;
            if (!IsGenuinelyFree(model)) return;
            if (!seen.Add(model.Id)) return;
            candidates.Add(model);
        }

        Add(_modelCacheService.GetSelectedFreeModel());
        foreach (var model in _modelCacheService.GetFreeModels())
        {
            if (candidates.Count >= limit) break;
            Add(model);
        }

        return candidates;
    }

    /// <summary>
    /// A model only counts as free when both its prices parse to zero. The ":free"
    /// suffix alone is not trusted here, because this is the guard standing between
    /// the free lane and a bill.
    /// </summary>
    private static bool IsGenuinelyFree(CachedModel model)
    {
        return decimal.TryParse(model.PromptPrice, NumberStyles.Float, CultureInfo.InvariantCulture, out var prompt)
               && decimal.TryParse(model.CompletionPrice, NumberStyles.Float, CultureInfo.InvariantCulture, out var completion)
               && prompt == 0m
               && completion == 0m;
    }

    private async Task<ChatCompletionResult> TryProviderWithRetryAsync(
        IAiProvider provider,
        string modelId,
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
                modelId,
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

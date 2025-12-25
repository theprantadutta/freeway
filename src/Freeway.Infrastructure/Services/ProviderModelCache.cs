using Freeway.Domain.Entities;
using Freeway.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Freeway.Infrastructure.Services;

/// <summary>
/// Thread-safe in-memory cache for provider models with validation and change detection
/// </summary>
public class ProviderModelCache : IProviderModelCache
{
    private readonly ILogger<ProviderModelCache> _logger;
    private readonly object _lock = new();

    // Provider name -> list of models
    private readonly Dictionary<string, List<ProviderModelInfo>> _providerModels = new();

    // Provider name -> last validation time
    private readonly Dictionary<string, DateTime> _lastValidated = new();

    // Model ID -> list of provider names (for reverse lookup)
    private readonly Dictionary<string, HashSet<string>> _modelToProviders = new();

    public ProviderModelCache(ILogger<ProviderModelCache> logger)
    {
        _logger = logger;
    }

    public List<ProviderModelInfo> GetModels(string providerName)
    {
        lock (_lock)
        {
            return _providerModels.TryGetValue(providerName, out var models)
                ? models.ToList()
                : new List<ProviderModelInfo>();
        }
    }

    public Dictionary<string, List<ProviderModelInfo>> GetAllProviderModels()
    {
        lock (_lock)
        {
            return _providerModels.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.ToList());
        }
    }

    public bool IsValidModel(string providerName, string modelId)
    {
        if (string.IsNullOrEmpty(providerName) || string.IsNullOrEmpty(modelId))
            return false;

        lock (_lock)
        {
            if (!_providerModels.TryGetValue(providerName, out var models))
            {
                // Provider not in cache - allow pass-through for uncached providers
                _logger.LogDebug("Provider '{Provider}' not in cache, allowing model '{Model}' pass-through",
                    providerName, modelId);
                return true;
            }

            return models.Any(m =>
                m.Id.Equals(modelId, StringComparison.OrdinalIgnoreCase) &&
                m.IsAvailable);
        }
    }

    public List<string> FindProvidersForModel(string modelId)
    {
        if (string.IsNullOrEmpty(modelId))
            return new List<string>();

        lock (_lock)
        {
            // Check the reverse index first
            if (_modelToProviders.TryGetValue(modelId.ToLowerInvariant(), out var providers))
            {
                return providers.ToList();
            }

            // Fall back to searching all providers (for case-insensitive match)
            var result = new List<string>();
            foreach (var (providerName, models) in _providerModels)
            {
                if (models.Any(m => m.Id.Equals(modelId, StringComparison.OrdinalIgnoreCase) && m.IsAvailable))
                {
                    result.Add(providerName);
                }
            }
            return result;
        }
    }

    public ModelChangeResult UpdateModels(string providerName, List<ProviderModelInfo> models)
    {
        lock (_lock)
        {
            var previousModels = _providerModels.TryGetValue(providerName, out var existing)
                ? existing
                : new List<ProviderModelInfo>();

            var previousIds = previousModels.Select(m => m.Id.ToLowerInvariant()).ToHashSet();
            var newIds = models.Select(m => m.Id.ToLowerInvariant()).ToHashSet();

            var addedIds = newIds.Except(previousIds).ToHashSet();
            var removedIds = previousIds.Except(newIds).ToHashSet();

            var added = models.Where(m => addedIds.Contains(m.Id.ToLowerInvariant())).ToList();
            var removed = previousModels.Where(m => removedIds.Contains(m.Id.ToLowerInvariant())).ToList();

            // Set provider name on all models
            foreach (var model in models)
            {
                model.ProviderName = providerName;
            }

            // Update the cache
            _providerModels[providerName] = models;
            _lastValidated[providerName] = DateTime.UtcNow;

            // Update the reverse index
            UpdateReverseIndex(providerName, previousModels, models);

            var result = new ModelChangeResult
            {
                Added = added,
                Removed = removed,
                TotalCount = models.Count
            };

            if (result.HasChanges)
            {
                _logger.LogInformation(
                    "Provider '{Provider}' models updated: +{Added} added, -{Removed} removed, {Total} total",
                    providerName, added.Count, removed.Count, models.Count);
            }
            else
            {
                _logger.LogDebug("Provider '{Provider}' models unchanged: {Total} total",
                    providerName, models.Count);
            }

            return result;
        }
    }

    public DateTime? GetLastValidated(string providerName)
    {
        lock (_lock)
        {
            return _lastValidated.TryGetValue(providerName, out var dt) ? dt : null;
        }
    }

    public ProviderModelCacheSummary GetCacheSummary()
    {
        lock (_lock)
        {
            var summary = new ProviderModelCacheSummary
            {
                ProviderCount = _providerModels.Count,
                TotalModelCount = _providerModels.Values.Sum(m => m.Count)
            };

            foreach (var (provider, models) in _providerModels)
            {
                summary.ModelCountByProvider[provider] = models.Count;
                summary.LastValidatedByProvider[provider] = _lastValidated.TryGetValue(provider, out var dt) ? dt : null;
            }

            return summary;
        }
    }

    public ProviderModelInfo? GetBestModel(string providerName)
    {
        lock (_lock)
        {
            if (!_providerModels.TryGetValue(providerName, out var models) || models.Count == 0)
            {
                _logger.LogDebug("No models cached for provider '{Provider}'", providerName);
                return null;
            }

            // Filter to chat-capable models and score them
            var scoredModels = models
                .Where(m => m.IsAvailable && IsChatCapableModel(m))
                .Select(m => new { Model = m, Score = CalculateModelScore(m, providerName) })
                .OrderByDescending(x => x.Score)
                .ToList();

            if (scoredModels.Count == 0)
            {
                _logger.LogWarning("No chat-capable models found for provider '{Provider}' among {Count} models",
                    providerName, models.Count);
                return null;
            }

            var best = scoredModels.First();
            _logger.LogDebug("Selected best model for '{Provider}': {Model} (score: {Score})",
                providerName, best.Model.Id, best.Score);

            return best.Model;
        }
    }

    public string? GetBestModelId(string providerName)
    {
        return GetBestModel(providerName)?.Id;
    }

    /// <summary>
    /// Determines if a model is suitable for chat completions
    /// </summary>
    private static bool IsChatCapableModel(ProviderModelInfo model)
    {
        var id = model.Id.ToLowerInvariant();
        var name = (model.Name ?? "").ToLowerInvariant();

        // Exclude non-chat models
        var excludePatterns = new[]
        {
            "embed", "embedding", "moderation", "whisper", "tts", "dall-e",
            "audio", "realtime", "vision-preview", "search", "similarity",
            "code-search", "text-search", "safeguard", "guard", "shield",
            "rerank", "classify", "tokenize"
        };

        foreach (var pattern in excludePatterns)
        {
            if (id.Contains(pattern) || name.Contains(pattern))
                return false;
        }

        // For OpenAI, only include known chat model prefixes
        if (id.StartsWith("gpt-") || id.StartsWith("o1") || id.StartsWith("o3") || id.StartsWith("chatgpt"))
            return true;

        // Include models with chat/instruct indicators
        var includePatterns = new[] { "chat", "instruct", "turbo", "assistant" };
        foreach (var pattern in includePatterns)
        {
            if (id.Contains(pattern) || name.Contains(pattern))
                return true;
        }

        // Provider-specific patterns for known chat models
        var chatModelPrefixes = new[]
        {
            "gemini-", "claude-", "llama-", "mixtral-", "mistral-", "command-",
            "qwen", "phi-", "deepseek-", "palm-", "codellama", "wizardlm"
        };

        foreach (var prefix in chatModelPrefixes)
        {
            if (id.Contains(prefix))
                return true;
        }

        // Default: assume it's chat-capable if we're not sure
        return true;
    }

    /// <summary>
    /// Calculates a score for model selection (higher is better)
    /// </summary>
    private int CalculateModelScore(ProviderModelInfo model, string providerName)
    {
        var id = model.Id.ToLowerInvariant();
        var score = 0;

        // Base score from context length (1 point per 10k context)
        score += (model.ContextLength ?? 0) / 10000;

        // Recency bonus (models created in last 6 months get bonus)
        if (model.CreatedAt.HasValue)
        {
            var monthsAgo = (DateTime.UtcNow - model.CreatedAt.Value).TotalDays / 30;
            if (monthsAgo < 6)
                score += (int)(50 - monthsAgo * 8); // Up to 50 points for very recent models
        }

        // Provider-specific flagship model bonuses
        score += GetFlagshipBonus(id, providerName);

        // Prefer instruct/chat variants
        if (id.Contains("instruct") || id.Contains("chat"))
            score += 20;

        // Prefer non-preview/experimental models for stability
        if (id.Contains("preview") || id.Contains("experimental") || id.Contains("exp"))
            score -= 10;

        // Prefer latest versions
        if (id.Contains("latest"))
            score += 15;

        // Penalize very old model versions
        if (id.Contains("0301") || id.Contains("0314") || id.Contains("0613"))
            score -= 30;

        return score;
    }

    /// <summary>
    /// Returns bonus points for flagship/recommended models per provider
    /// </summary>
    private static int GetFlagshipBonus(string modelId, string providerName)
    {
        return providerName.ToLowerInvariant() switch
        {
            "openai" => modelId switch
            {
                var id when id.Contains("gpt-4o") && !id.Contains("mini") => 100,
                var id when id.Contains("gpt-4o-mini") => 80,
                var id when id.Contains("o1") || id.Contains("o3") => 90,
                var id when id.Contains("gpt-4-turbo") => 70,
                var id when id.Contains("gpt-4") => 60,
                var id when id.Contains("gpt-3.5-turbo") => 40,
                _ => 0
            },
            "gemini" => modelId switch
            {
                var id when id.Contains("gemini-2") && id.Contains("flash") => 100,
                var id when id.Contains("gemini-2") && id.Contains("pro") => 95,
                var id when id.Contains("gemini-1.5-pro") => 80,
                var id when id.Contains("gemini-1.5-flash") => 70,
                var id when id.Contains("gemini-pro") => 50,
                _ => 0
            },
            "groq" => modelId switch
            {
                var id when id.Contains("llama-3.3-70b") => 100,
                var id when id.Contains("llama-3.1-70b") => 90,
                var id when id.Contains("mixtral-8x7b") => 80,
                var id when id.Contains("llama-3") => 70,
                var id when id.Contains("gemma") => 60,
                _ => 0
            },
            "mistral" => modelId switch
            {
                var id when id.Contains("mistral-large") => 100,
                var id when id.Contains("mistral-medium") => 80,
                var id when id.Contains("mistral-small") => 60,
                var id when id.Contains("mixtral") => 70,
                var id when id.Contains("codestral") => 50,
                _ => 0
            },
            "cohere" => modelId switch
            {
                var id when id.Contains("command-a") => 100,
                var id when id.Contains("command-r-plus") => 90,
                var id when id.Contains("command-r") => 80,
                var id when id.Contains("command") => 60,
                _ => 0
            },
            "huggingface" => modelId switch
            {
                var id when id.Contains("llama-3.2") => 100,
                var id when id.Contains("llama-3.1") => 90,
                var id when id.Contains("mistral") => 80,
                var id when id.Contains("qwen") => 70,
                _ => 0
            },
            "openrouter" => modelId switch
            {
                // OpenRouter uses full provider/model format
                var id when id.Contains("claude-3") && id.Contains("opus") => 100,
                var id when id.Contains("gpt-4o") => 95,
                var id when id.Contains("claude-3") && id.Contains("sonnet") => 90,
                var id when id.Contains("gemini-2") => 85,
                var id when id.Contains(":free") => 50, // Free models get moderate bonus
                _ => 0
            },
            _ => 0
        };
    }

    private void UpdateReverseIndex(string providerName, List<ProviderModelInfo> previousModels, List<ProviderModelInfo> newModels)
    {
        // Remove old entries
        foreach (var model in previousModels)
        {
            var key = model.Id.ToLowerInvariant();
            if (_modelToProviders.TryGetValue(key, out var providers))
            {
                providers.Remove(providerName);
                if (providers.Count == 0)
                {
                    _modelToProviders.Remove(key);
                }
            }
        }

        // Add new entries
        foreach (var model in newModels)
        {
            if (!model.IsAvailable) continue;

            var key = model.Id.ToLowerInvariant();
            if (!_modelToProviders.TryGetValue(key, out var providers))
            {
                providers = new HashSet<string>();
                _modelToProviders[key] = providers;
            }
            providers.Add(providerName);
        }
    }
}

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

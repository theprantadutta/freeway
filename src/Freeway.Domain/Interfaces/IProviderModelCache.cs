using Freeway.Domain.Entities;

namespace Freeway.Domain.Interfaces;

/// <summary>
/// Cache for storing and validating models from all AI providers
/// </summary>
public interface IProviderModelCache
{
    /// <summary>
    /// Gets all cached models for a specific provider
    /// </summary>
    /// <param name="providerName">The provider name</param>
    /// <returns>List of models for the provider</returns>
    List<ProviderModelInfo> GetModels(string providerName);

    /// <summary>
    /// Gets all cached models from all providers
    /// </summary>
    /// <returns>Dictionary of provider name to models</returns>
    Dictionary<string, List<ProviderModelInfo>> GetAllProviderModels();

    /// <summary>
    /// Checks if a model ID is valid for a specific provider
    /// </summary>
    /// <param name="providerName">The provider name</param>
    /// <param name="modelId">The model ID to check</param>
    /// <returns>True if the model is valid for the provider</returns>
    bool IsValidModel(string providerName, string modelId);

    /// <summary>
    /// Finds which provider(s) support a given model ID
    /// </summary>
    /// <param name="modelId">The model ID to find</param>
    /// <returns>List of provider names that support the model</returns>
    List<string> FindProvidersForModel(string modelId);

    /// <summary>
    /// Updates the cached models for a provider, detecting additions and removals
    /// </summary>
    /// <param name="providerName">The provider name</param>
    /// <param name="models">The new list of models</param>
    /// <returns>Result containing added and removed models</returns>
    ModelChangeResult UpdateModels(string providerName, List<ProviderModelInfo> models);

    /// <summary>
    /// Gets the last validation time for a provider
    /// </summary>
    /// <param name="providerName">The provider name</param>
    /// <returns>The last validation time, or null if never validated</returns>
    DateTime? GetLastValidated(string providerName);

    /// <summary>
    /// Gets a summary of the cache state
    /// </summary>
    /// <returns>Summary of providers and model counts</returns>
    ProviderModelCacheSummary GetCacheSummary();

    /// <summary>
    /// Gets the best available chat model for a provider based on cached model data
    /// </summary>
    /// <param name="providerName">The provider name</param>
    /// <returns>The best model info, or null if no models available</returns>
    ProviderModelInfo? GetBestModel(string providerName);

    /// <summary>
    /// Gets the best model ID for a provider (convenience method)
    /// </summary>
    /// <param name="providerName">The provider name</param>
    /// <returns>The best model ID, or null if no models available</returns>
    string? GetBestModelId(string providerName);
}

/// <summary>
/// Summary of the provider model cache state
/// </summary>
public class ProviderModelCacheSummary
{
    /// <summary>
    /// Number of providers in the cache
    /// </summary>
    public int ProviderCount { get; set; }

    /// <summary>
    /// Total number of models across all providers
    /// </summary>
    public int TotalModelCount { get; set; }

    /// <summary>
    /// Model count per provider
    /// </summary>
    public Dictionary<string, int> ModelCountByProvider { get; set; } = new();

    /// <summary>
    /// Last validation time per provider
    /// </summary>
    public Dictionary<string, DateTime?> LastValidatedByProvider { get; set; } = new();
}

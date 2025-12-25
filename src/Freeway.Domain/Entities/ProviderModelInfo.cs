namespace Freeway.Domain.Entities;

/// <summary>
/// Represents a model available on a specific AI provider
/// </summary>
public class ProviderModelInfo
{
    /// <summary>
    /// The model ID as used by the provider (e.g., "gpt-4o-mini", "gemini-2.0-flash-exp")
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable model name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The provider that hosts this model
    /// </summary>
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>
    /// Optional description of the model
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Context window size in tokens (if available)
    /// </summary>
    public int? ContextLength { get; set; }

    /// <summary>
    /// Whether this model is currently available
    /// </summary>
    public bool IsAvailable { get; set; } = true;

    /// <summary>
    /// When the model was created (if provided by API)
    /// </summary>
    public DateTime? CreatedAt { get; set; }

    /// <summary>
    /// Owner of the model (e.g., "openai", "meta")
    /// </summary>
    public string? OwnedBy { get; set; }
}

/// <summary>
/// Result of fetching models from a provider
/// </summary>
public class ProviderModelListResult
{
    /// <summary>
    /// Whether the fetch operation succeeded
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if the fetch failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// List of models fetched from the provider
    /// </summary>
    public List<ProviderModelInfo> Models { get; set; } = new();

    /// <summary>
    /// Time taken to fetch models in milliseconds
    /// </summary>
    public int ResponseTimeMs { get; set; }

    public static ProviderModelListResult CreateSuccess(List<ProviderModelInfo> models, int responseTimeMs)
    {
        return new ProviderModelListResult
        {
            Success = true,
            Models = models,
            ResponseTimeMs = responseTimeMs
        };
    }

    public static ProviderModelListResult CreateError(string errorMessage, int responseTimeMs)
    {
        return new ProviderModelListResult
        {
            Success = false,
            ErrorMessage = errorMessage,
            ResponseTimeMs = responseTimeMs
        };
    }
}

/// <summary>
/// Result of updating models in the cache
/// </summary>
public class ModelChangeResult
{
    /// <summary>
    /// Models that were added since the last update
    /// </summary>
    public List<ProviderModelInfo> Added { get; set; } = new();

    /// <summary>
    /// Models that were removed since the last update
    /// </summary>
    public List<ProviderModelInfo> Removed { get; set; } = new();

    /// <summary>
    /// Total number of models now in cache for this provider
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Whether there were any changes
    /// </summary>
    public bool HasChanges => Added.Count > 0 || Removed.Count > 0;
}

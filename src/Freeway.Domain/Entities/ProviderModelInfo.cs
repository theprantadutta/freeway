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

    /// <summary>HTTP status from the provider, when the failure was an HTTP one.</summary>
    public int? HttpStatusCode { get; set; }

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

    public static ProviderModelListResult CreateError(
        string errorMessage,
        int responseTimeMs,
        int? httpStatusCode = null)
    {
        return new ProviderModelListResult
        {
            Success = false,
            ErrorMessage = errorMessage,
            ResponseTimeMs = responseTimeMs,
            HttpStatusCode = httpStatusCode
        };
    }

    /// <summary>
    /// True when the provider rejected the credential rather than failing for some
    /// transient reason. 401/403 are the usual shape; Gemini answers 400 with
    /// API_KEY_INVALID, which is a rejection too.
    /// </summary>
    public bool IsCredentialFailure
    {
        get
        {
            if (Success) return false;
            if (HttpStatusCode is 401 or 403) return true;

            // A bare 400 is not enough: it is just as likely to be a malformed
            // request. Only treat it as a rejection when the body says so, which is
            // how Gemini reports a bad key.
            var message = ErrorMessage ?? "";
            return message.Contains("API_KEY_INVALID", StringComparison.OrdinalIgnoreCase)
                   || message.Contains("API key not valid", StringComparison.OrdinalIgnoreCase)
                   || message.Contains("invalid_api_key", StringComparison.OrdinalIgnoreCase)
                   || message.Contains("Incorrect API key", StringComparison.OrdinalIgnoreCase)
                   || message.Contains("invalid authentication", StringComparison.OrdinalIgnoreCase);
        }
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

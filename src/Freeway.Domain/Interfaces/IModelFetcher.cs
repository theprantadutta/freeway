using Freeway.Domain.Entities;

namespace Freeway.Domain.Interfaces;

/// <summary>
/// Interface for providers that can fetch their available models from their native API
/// </summary>
public interface IModelFetcher
{
    /// <summary>
    /// The provider name this fetcher is associated with
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Whether this fetcher can currently fetch models (has API key configured)
    /// </summary>
    bool CanFetch { get; }

    /// <summary>
    /// Fetch available models from the provider's native API
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing the list of available models</returns>
    Task<ProviderModelListResult> FetchModelsAsync(CancellationToken cancellationToken = default);
}

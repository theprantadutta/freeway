using Freeway.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Freeway.Infrastructure.Jobs;

public interface IModelValidationJob
{
    Task ValidateModelsAsync();
}

public class ModelValidationJob : IModelValidationJob
{
    private readonly IEnumerable<IModelFetcher> _modelFetchers;
    private readonly IProviderModelCache _providerModelCache;
    private readonly ILogger<ModelValidationJob> _logger;

    private readonly int _maxConcurrency;
    private readonly int _delayBetweenProviders;

    public ModelValidationJob(
        IEnumerable<IModelFetcher> modelFetchers,
        IProviderModelCache providerModelCache,
        ILogger<ModelValidationJob> logger)
    {
        _modelFetchers = modelFetchers;
        _providerModelCache = providerModelCache;
        _logger = logger;

        _maxConcurrency = int.TryParse(
            Environment.GetEnvironmentVariable("MODEL_VALIDATION_CONCURRENCY"),
            out var c) ? c : 3;
        _delayBetweenProviders = int.TryParse(
            Environment.GetEnvironmentVariable("MODEL_VALIDATION_DELAY_MS"),
            out var d) ? d : 500;
    }

    public async Task ValidateModelsAsync()
    {
        _logger.LogInformation("Starting daily model validation run...");

        var enabledFetchers = _modelFetchers.Where(f => f.CanFetch).ToList();

        _logger.LogInformation("Validating models for {Count} enabled providers: {Providers}",
            enabledFetchers.Count,
            string.Join(", ", enabledFetchers.Select(f => f.ProviderName)));

        var results = new List<(string Provider, bool Success, int ModelCount, string? Error)>();

        // Use semaphore for controlled concurrency
        using var semaphore = new SemaphoreSlim(_maxConcurrency);

        var tasks = enabledFetchers.Select(async fetcher =>
        {
            await semaphore.WaitAsync();
            try
            {
                // Add delay between requests to respect rate limits
                await Task.Delay(_delayBetweenProviders);
                return await ValidateProviderModelsAsync(fetcher);
            }
            finally
            {
                semaphore.Release();
            }
        });

        var taskResults = await Task.WhenAll(tasks);
        results.AddRange(taskResults);

        // Log summary
        LogValidationSummary(results);
    }

    private async Task<(string Provider, bool Success, int ModelCount, string? Error)> ValidateProviderModelsAsync(IModelFetcher fetcher)
    {
        try
        {
            _logger.LogInformation("Fetching models from {Provider}...", fetcher.ProviderName);

            var result = await fetcher.FetchModelsAsync();

            if (!result.Success)
            {
                _logger.LogWarning(
                    "{Provider}: Failed to fetch models - {Error}. Keeping previous cache.",
                    fetcher.ProviderName, result.ErrorMessage);

                return (fetcher.ProviderName, false, 0, result.ErrorMessage);
            }

            if (result.Models.Count == 0)
            {
                _logger.LogWarning(
                    "{Provider}: API returned empty model list. Keeping previous cache.",
                    fetcher.ProviderName);

                return (fetcher.ProviderName, false, 0, "Empty model list returned");
            }

            // Update cache and get changes
            var changes = _providerModelCache.UpdateModels(fetcher.ProviderName, result.Models);

            // Log changes
            if (changes.Added.Count > 0)
            {
                _logger.LogInformation("{Provider}: Added {Count} new models: {Models}",
                    fetcher.ProviderName,
                    changes.Added.Count,
                    string.Join(", ", changes.Added.Take(10).Select(m => m.Id)));
            }

            if (changes.Removed.Count > 0)
            {
                _logger.LogWarning("{Provider}: Removed {Count} models: {Models}",
                    fetcher.ProviderName,
                    changes.Removed.Count,
                    string.Join(", ", changes.Removed.Take(10).Select(m => m.Id)));
            }

            _logger.LogInformation("{Provider}: Validated {Count} models in {Time}ms",
                fetcher.ProviderName, changes.TotalCount, result.ResponseTimeMs);

            // Log the best model selected for this provider
            var bestModel = _providerModelCache.GetBestModel(fetcher.ProviderName);
            if (bestModel != null)
            {
                _logger.LogInformation("{Provider}: Best model selected: {Model} (context: {Context})",
                    fetcher.ProviderName, bestModel.Id, bestModel.ContextLength ?? 0);
            }

            return (fetcher.ProviderName, true, changes.TotalCount, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Provider}: Unexpected error. Keeping previous cache.",
                fetcher.ProviderName);
            return (fetcher.ProviderName, false, 0, ex.Message);
        }
    }

    private void LogValidationSummary(
        List<(string Provider, bool Success, int ModelCount, string? Error)> results)
    {
        var successful = results.Where(r => r.Success).ToList();
        var failed = results.Where(r => !r.Success).ToList();

        var totalModels = successful.Sum(r => r.ModelCount);

        _logger.LogInformation(
            "Model validation complete: {Success}/{Total} providers validated. Total models: {ModelCount}",
            successful.Count, results.Count, totalModels);

        if (failed.Count > 0)
        {
            _logger.LogWarning("Failed providers: {Providers}",
                string.Join(", ", failed.Select(f => $"{f.Provider} ({f.Error})")));
        }

        // Log cache summary
        var summary = _providerModelCache.GetCacheSummary();
        _logger.LogInformation("Cache summary: {ProviderCount} providers, {ModelCount} total models",
            summary.ProviderCount, summary.TotalModelCount);
    }
}

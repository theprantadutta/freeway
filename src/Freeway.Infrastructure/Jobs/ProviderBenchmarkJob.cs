using Freeway.Domain.Entities;
using Freeway.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Freeway.Infrastructure.Jobs;

public interface IProviderBenchmarkJob
{
    Task RunBenchmarkAsync();
}

public class ProviderBenchmarkJob : IProviderBenchmarkJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IEnumerable<IAiProvider> _providers;
    private readonly IProviderBenchmarkCache _benchmarkCache;
    private readonly ILogger<ProviderBenchmarkJob> _logger;

    private static readonly List<ChatMessage> TestMessages =
    [
        new() { Role = "user", Content = "Say hello in 5 words or less" }
    ];

    private static readonly ChatCompletionOptions TestOptions = new()
    {
        MaxTokens = 50,
        Temperature = 0.7
    };

    public ProviderBenchmarkJob(
        IServiceScopeFactory scopeFactory,
        IEnumerable<IAiProvider> providers,
        IProviderBenchmarkCache benchmarkCache,
        ILogger<ProviderBenchmarkJob> logger)
    {
        _scopeFactory = scopeFactory;
        _providers = providers;
        _benchmarkCache = benchmarkCache;
        _logger = logger;
    }

    public async Task RunBenchmarkAsync()
    {
        _logger.LogInformation("Starting provider benchmark run...");

        var results = new List<ProviderBenchmark>();

        foreach (var provider in _providers)
        {
            if (!provider.IsEnabled)
            {
                _logger.LogDebug("Skipping {Provider}: not configured", provider.Name);
                continue;
            }

            if (!provider.IsFreeProvider)
            {
                _logger.LogDebug("Skipping {Provider}: paid provider (not benchmarked)", provider.Name);
                continue;
            }

            try
            {
                _logger.LogInformation("Benchmarking {Provider} ({Model})...",
                    provider.DisplayName, provider.DefaultModelId);

                var result = await provider.CreateChatCompletionAsync(
                    provider.DefaultModelId,
                    TestMessages,
                    TestOptions,
                    CancellationToken.None);

                var benchmark = new ProviderBenchmark
                {
                    Id = Guid.NewGuid(),
                    ProviderName = provider.Name,
                    ModelId = provider.DefaultModelId,
                    ResponseTimeMs = result.ResponseTimeMs,
                    Success = result.Success,
                    ErrorMessage = result.ErrorMessage,
                    ErrorCode = result.HttpStatusCode,
                    TestedAt = DateTime.UtcNow
                };

                results.Add(benchmark);

                // Update in-memory cache immediately
                _benchmarkCache.AddBenchmarkResult(provider.Name, result.ResponseTimeMs, result.Success);

                if (result.Success)
                {
                    _logger.LogInformation("{Provider}: SUCCESS in {Time}ms",
                        provider.Name, result.ResponseTimeMs);
                }
                else
                {
                    _logger.LogWarning("{Provider}: FAILED - {Error} (HTTP {StatusCode})",
                        provider.Name, result.ErrorMessage, result.HttpStatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error benchmarking {Provider}", provider.Name);

                results.Add(new ProviderBenchmark
                {
                    Id = Guid.NewGuid(),
                    ProviderName = provider.Name,
                    ModelId = provider.DefaultModelId,
                    ResponseTimeMs = 0,
                    Success = false,
                    ErrorMessage = ex.Message,
                    TestedAt = DateTime.UtcNow
                });
            }
        }

        // Save all results to database
        if (results.Count > 0)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

                context.ProviderBenchmarks.AddRange(results);
                await context.SaveChangesAsync();

                _logger.LogInformation("Saved {Count} benchmark results to database", results.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save benchmark results to database");
            }
        }

        // Log summary
        var successful = results.Count(r => r.Success);
        var failed = results.Count(r => !r.Success);
        _logger.LogInformation("Benchmark run complete: {Success} succeeded, {Failed} failed",
            successful, failed);

        // Log current rankings
        var rankings = _benchmarkCache.GetRankedProviders();
        _logger.LogInformation("Current provider rankings: {Rankings}",
            string.Join(" > ", rankings));
    }
}

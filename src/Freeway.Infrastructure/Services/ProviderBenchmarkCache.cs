using Freeway.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Freeway.Infrastructure.Services;

public class ProviderBenchmarkCache : IProviderBenchmarkCache
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ProviderBenchmarkCache> _logger;
    private readonly object _lock = new();

    // Default provider order when no benchmarks available
    private static readonly string[] DefaultProviderOrder =
    [
        "gemini",
        "groq",
        "mistral",
        "cohere",
        "huggingface"
    ];

    private Dictionary<string, ProviderScore> _scores = new();
    private List<string> _rankedProviders = new(DefaultProviderOrder);

    public ProviderBenchmarkCache(
        IServiceScopeFactory scopeFactory,
        ILogger<ProviderBenchmarkCache> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public List<string> GetRankedProviders()
    {
        lock (_lock)
        {
            return new List<string>(_rankedProviders);
        }
    }

    public ProviderScore? GetProviderScore(string providerName)
    {
        lock (_lock)
        {
            return _scores.TryGetValue(providerName, out var score) ? score : null;
        }
    }

    public void AddBenchmarkResult(string providerName, int responseTimeMs, bool success)
    {
        lock (_lock)
        {
            if (!_scores.TryGetValue(providerName, out var score))
            {
                score = new ProviderScore { ProviderName = providerName };
                _scores[providerName] = score;
            }

            // Update rolling average (simple moving average)
            var totalResponseTime = score.AvgResponseTimeMs * score.TotalTests;
            var successCount = (int)(score.SuccessRate * score.TotalTests);

            score.TotalTests++;
            if (success)
            {
                successCount++;
                totalResponseTime += responseTimeMs;
            }

            score.AvgResponseTimeMs = totalResponseTime / score.TotalTests;
            score.SuccessRate = (double)successCount / score.TotalTests;

            // Recalculate score: (SuccessRate * 100) - (AvgResponseTimeMs / 100)
            score.Score = (score.SuccessRate * 100) - (score.AvgResponseTimeMs / 100);

            // Re-rank providers
            RecalculateRankings();
        }
    }

    public async Task RefreshFromDatabaseAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

            // Get benchmarks from last 24 hours
            var cutoff = DateTime.UtcNow.AddHours(-24);

            var benchmarks = await context.ProviderBenchmarks
                .Where(b => b.TestedAt >= cutoff)
                .GroupBy(b => b.ProviderName)
                .Select(g => new
                {
                    ProviderName = g.Key,
                    AvgResponseTimeMs = g.Where(x => x.Success).Average(x => (double?)x.ResponseTimeMs) ?? 0,
                    SuccessRate = g.Count(x => x.Success) / (double)g.Count(),
                    TotalTests = g.Count()
                })
                .ToListAsync();

            lock (_lock)
            {
                _scores.Clear();

                foreach (var b in benchmarks)
                {
                    var score = new ProviderScore
                    {
                        ProviderName = b.ProviderName,
                        AvgResponseTimeMs = b.AvgResponseTimeMs,
                        SuccessRate = b.SuccessRate,
                        TotalTests = b.TotalTests,
                        Score = (b.SuccessRate * 100) - (b.AvgResponseTimeMs / 100)
                    };
                    _scores[b.ProviderName] = score;
                }

                RecalculateRankings();
            }

            _logger.LogInformation("Refreshed benchmark cache with {Count} providers", benchmarks.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh benchmark cache from database");
        }
    }

    private void RecalculateRankings()
    {
        // Start with all default providers
        var allProviders = new HashSet<string>(DefaultProviderOrder);

        // Add any providers from scores that aren't in defaults
        foreach (var providerName in _scores.Keys)
        {
            allProviders.Add(providerName);
        }

        // Sort by score (highest first), fall back to default order for providers without scores
        _rankedProviders = allProviders
            .OrderByDescending(p =>
            {
                if (_scores.TryGetValue(p, out var score))
                {
                    // Providers with very low success rate should be deprioritized
                    if (score.SuccessRate < 0.3)
                        return double.MinValue;
                    return score.Score;
                }
                // Providers without scores get default priority based on position in default list
                var defaultIndex = Array.IndexOf(DefaultProviderOrder, p);
                return defaultIndex >= 0 ? 50 - defaultIndex : 0;
            })
            .ToList();

        _logger.LogDebug("Provider rankings updated: {Rankings}",
            string.Join(", ", _rankedProviders.Select((p, i) =>
            {
                var scoreStr = _scores.TryGetValue(p, out var s) ? $"{s.Score:F1}" : "N/A";
                return $"{i + 1}. {p} ({scoreStr})";
            })));
    }
}

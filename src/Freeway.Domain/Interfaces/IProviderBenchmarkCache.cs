namespace Freeway.Domain.Interfaces;

public interface IProviderBenchmarkCache
{
    /// <summary>
    /// Get providers ranked by benchmark score (best first)
    /// </summary>
    List<string> GetRankedProviders();

    /// <summary>
    /// Get the benchmark score for a specific provider
    /// </summary>
    ProviderScore? GetProviderScore(string providerName);

    /// <summary>
    /// Refresh cache from database
    /// </summary>
    Task RefreshFromDatabaseAsync();

    /// <summary>
    /// Update cache with a new benchmark result
    /// </summary>
    void AddBenchmarkResult(string providerName, int responseTimeMs, bool success);
}

public class ProviderScore
{
    public string ProviderName { get; set; } = string.Empty;
    public double AvgResponseTimeMs { get; set; }
    public double SuccessRate { get; set; }
    public int TotalTests { get; set; }
    public double Score { get; set; }
}

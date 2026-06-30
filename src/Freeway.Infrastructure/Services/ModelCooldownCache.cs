using System.Collections.Concurrent;
using Freeway.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Freeway.Infrastructure.Services;

/// <summary>
/// In-memory cooldown tracker for rate-limited models. When a model returns a 429 it is
/// parked for <c>MODEL_COOLDOWN_SECONDS</c> (default 60s) so the fallback chain skips it
/// until the window expires.
/// </summary>
public class ModelCooldownCache : IModelCooldownCache
{
    private readonly ILogger<ModelCooldownCache> _logger;
    private readonly TimeSpan _cooldown;

    // modelId -> UTC time when the cooldown expires
    private readonly ConcurrentDictionary<string, DateTime> _cooldowns = new(StringComparer.OrdinalIgnoreCase);

    public ModelCooldownCache(ILogger<ModelCooldownCache> logger)
    {
        _logger = logger;
        var seconds = int.TryParse(Environment.GetEnvironmentVariable("MODEL_COOLDOWN_SECONDS"), out var s) && s > 0
            ? s
            : 60;
        _cooldown = TimeSpan.FromSeconds(seconds);
    }

    public void MarkRateLimited(string modelId)
    {
        if (string.IsNullOrEmpty(modelId))
            return;

        var expiresAt = DateTime.UtcNow.Add(_cooldown);
        _cooldowns[modelId] = expiresAt;
        _logger.LogWarning("Model {Model} put on cooldown until {ExpiresAt:O} after rate limit", modelId, expiresAt);
    }

    public bool IsRateLimited(string modelId)
    {
        if (string.IsNullOrEmpty(modelId) || !_cooldowns.TryGetValue(modelId, out var expiresAt))
            return false;

        if (DateTime.UtcNow >= expiresAt)
        {
            // Cooldown elapsed - clean up and treat as available again.
            _cooldowns.TryRemove(modelId, out _);
            return false;
        }

        return true;
    }
}

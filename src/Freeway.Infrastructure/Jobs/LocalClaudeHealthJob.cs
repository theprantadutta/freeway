using Freeway.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Freeway.Infrastructure.Jobs;

public interface ILocalClaudeHealthJob
{
    Task CheckAsync();
}

/// <summary>
/// Keeps the verdict on local Claude fresh so the request path never has to probe.
///
/// Runs hourly by default. Between runs the verdict can still be downgraded by a real
/// request failing, which is the more useful signal anyway.
/// </summary>
public class LocalClaudeHealthJob : ILocalClaudeHealthJob
{
    private readonly ILocalClaudeService _localClaude;
    private readonly ILogger<LocalClaudeHealthJob> _logger;

    public LocalClaudeHealthJob(ILocalClaudeService localClaude, ILogger<LocalClaudeHealthJob> logger)
    {
        _localClaude = localClaude;
        _logger = logger;
    }

    public async Task CheckAsync()
    {
        if (!_localClaude.IsConfigured)
        {
            _logger.LogDebug("Local Claude is not configured; skipping the probe");
            return;
        }

        try
        {
            var health = await _localClaude.CheckHealthAsync();
            _logger.LogInformation("Local Claude probe: {Status}", health.Describe());
        }
        catch (Exception ex)
        {
            // A probe failure must never surface as a job failure: this route is
            // optional and the gateway carries on without it.
            _logger.LogWarning(ex, "Local Claude probe threw; treating it as unavailable");
        }
    }
}

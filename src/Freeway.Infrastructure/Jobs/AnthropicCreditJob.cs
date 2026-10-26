using Freeway.Domain.Interfaces;
using Freeway.Infrastructure.Services;
using Microsoft.Extensions.Logging;

namespace Freeway.Infrastructure.Jobs;

public interface IAnthropicCreditJob
{
    Task RefreshAsync();
}

/// <summary>
/// Keeps the credit count honest: opens a new cycle when the old one expires and
/// recounts the spend from the usage logs, which catches anything the in-memory
/// count missed (a restart, a second instance). Every five minutes by default.
/// </summary>
public class AnthropicCreditJob : IAnthropicCreditJob
{
    private readonly IAnthropicCreditService _credit;
    private readonly AnthropicKeyWatch _keys;
    private readonly ILogger<AnthropicCreditJob> _logger;

    public AnthropicCreditJob(IAnthropicCreditService credit, AnthropicKeyWatch keys, ILogger<AnthropicCreditJob> logger)
    {
        _credit = credit;
        _keys = keys;
        _logger = logger;
    }

    public async Task RefreshAsync()
    {
        if (!_credit.IsConfigured)
        {
            return;
        }

        try
        {
            var status = await _credit.RefreshAsync();
            _logger.LogInformation("Anthropic credit: {Status}", status.Describe());
        }
        catch (Exception ex)
        {
            // The credit is optional; a failed recount leaves the last count in place.
            _logger.LogWarning(ex, "Anthropic credit refresh failed; keeping the previous count");
        }

        // Expiry and rejection reminders for the key. Each is sent once, so running
        // this every few minutes costs nothing but a query.
        try
        {
            await _keys.CheckAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Anthropic key check failed");
        }
    }
}

using Freeway.Domain.Entities;

namespace Freeway.Domain.Interfaces;

/// <summary>
/// The premium lane's optional no-charge backend: Claude Code running on the host,
/// reached through the claude-bridge sidecar.
///
/// Every method is best-effort. Nothing here is allowed to fail a request: if the
/// bridge is missing, throttled, slow or strange, the caller carries on with a paid
/// model and the reason lands in the log.
/// </summary>
public interface ILocalClaudeService
{
    /// <summary>False when the feature is switched off or not configured at all.</summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Last known state, from the periodic probe. Never performs a network call, so
    /// it is safe to consult on the request path.
    /// </summary>
    LocalClaudeHealth Health { get; }

    /// <summary>Probes the bridge and updates <see cref="Health"/>.</summary>
    Task<LocalClaudeHealth> CheckHealthAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts a completion. Returns a result whose <see cref="ChatCompletionResult.Success"/>
    /// is false, with a reason, rather than throwing.
    /// </summary>
    Task<ChatCompletionResult> CompleteAsync(
        List<ChatMessage> messages,
        ChatCompletionOptions? options = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Holds the last known verdict. Separate from the service because the service is
/// created per request (it owns a typed HttpClient) while the verdict has to persist
/// between them, the same way the model and cooldown caches do.
/// </summary>
public interface ILocalClaudeHealthCache
{
    LocalClaudeHealth Current { get; }
    void Set(LocalClaudeHealth health);
}

public enum LocalClaudeState
{
    /// <summary>Never probed, or the feature is off.</summary>
    Unknown,
    Available,
    RateLimited,
    Unavailable
}

public class LocalClaudeHealth
{
    public LocalClaudeState State { get; set; } = LocalClaudeState.Unknown;

    /// <summary>Why it is not available, in the bridge's own words.</summary>
    public string? Detail { get; set; }

    public int? LatencyMs { get; set; }
    public string? Model { get; set; }
    public DateTime? CheckedAt { get; set; }

    public bool CanServe => State == LocalClaudeState.Available;

    /// <summary>One line for a log or an email, explaining the current verdict.</summary>
    public string Describe() => State switch
    {
        LocalClaudeState.Available => $"available ({Model ?? "unknown model"}, {LatencyMs ?? 0}ms probe)",
        LocalClaudeState.RateLimited => $"rate limited: {Detail ?? "no detail"}",
        LocalClaudeState.Unavailable => $"unavailable: {Detail ?? "no detail"}",
        _ => "not checked yet"
    };
}

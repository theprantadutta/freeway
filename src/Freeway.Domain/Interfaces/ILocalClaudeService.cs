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
        LocalClaudePriority priority = LocalClaudePriority.Premium,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// What a caller is entitled to ask of the subscription.
/// </summary>
public enum LocalClaudePriority
{
    /// <summary>
    /// Queues, waits, and may spend the whole hourly budget. The premium lane
    /// displaces frontier-model pricing, so this is where the subscription pays for
    /// itself and it is never held back for a cheaper lane.
    /// </summary>
    Premium,

    /// <summary>
    /// Rides along on spare capacity only. Refused the instant the bridge is busy or
    /// the spillover share of the budget is spent, and given a short deadline, so a
    /// cheap lane can never turn the subscription into latency for itself or crowd
    /// premium out of it.
    /// </summary>
    Spillover
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

    /// <summary>A call was in flight or queued when this was taken.</summary>
    public bool Busy { get; set; }

    /// <summary>
    /// Where the hourly token budget stands. The ceiling is configured rather than
    /// discovered -- the CLI reports what a call used but nothing about the plan's
    /// limit -- so this is how you find out whether yours is set sensibly.
    /// </summary>
    public LocalClaudeBudget? Budget { get; set; }

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

/// <summary>A rolling hour of subscription spend, as the bridge sees it.</summary>
public class LocalClaudeBudget
{
    public long Limit { get; set; }
    public long Used { get; set; }
    public long Remaining { get; set; }

    /// <summary>The part of the budget spillover may use; the rest is premium's.</summary>
    public long SpilloverLimit { get; set; }

    /// <summary>Tokens the bridge expects the next call to cost, learned from recent ones.</summary>
    public long Estimate { get; set; }

    public double UsedPercent => Limit == 0 ? 0 : (double)Used / Limit * 100;
}

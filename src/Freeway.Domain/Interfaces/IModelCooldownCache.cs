namespace Freeway.Domain.Interfaces;

/// <summary>
/// Tracks models that recently returned a rate-limit (429) response so the fallback
/// chain can skip them for a short cooldown window instead of re-hitting a known-throttled
/// model on every request.
/// </summary>
public interface IModelCooldownCache
{
    /// <summary>Marks a model as rate-limited, starting its cooldown window.</summary>
    void MarkRateLimited(string modelId);

    /// <summary>Returns true if the model is currently within its cooldown window.</summary>
    bool IsRateLimited(string modelId);
}

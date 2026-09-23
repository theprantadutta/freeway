using Freeway.Domain.Interfaces;

namespace Freeway.Infrastructure.Services;

/// <summary>
/// The last verdict on local Claude, shared across requests. Held in memory like the
/// other caches here: on restart it reads as "not checked yet", which makes the
/// premium lane use a paid model until the next probe, and that is the safe way round.
/// </summary>
public class LocalClaudeHealthCache : ILocalClaudeHealthCache
{
    private readonly object _lock = new();
    private LocalClaudeHealth _health = new();

    public LocalClaudeHealth Current
    {
        get
        {
            lock (_lock) return _health;
        }
    }

    public void Set(LocalClaudeHealth health)
    {
        lock (_lock) _health = health;
    }
}

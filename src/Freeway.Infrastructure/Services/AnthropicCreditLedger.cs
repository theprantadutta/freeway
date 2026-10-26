using Freeway.Domain.Entities;
using Freeway.Domain.Interfaces;

namespace Freeway.Infrastructure.Services;

/// <summary>
/// The running count of credit spent, shared by every request.
///
/// Separate from the service for the same reason the local-Claude verdict is: the
/// service is created per request, the count has to outlive them. It is refreshed
/// from the usage logs on a schedule and added to by each call in between, and only
/// ever moves up within a cycle, so a refresh racing a log write cannot make the
/// credit look less spent than it is.
/// </summary>
public sealed class AnthropicCreditLedger
{
    private readonly object _gate = new();

    private CreditGrant? _grant;
    private decimal _used;
    private bool _counted;
    private string? _blockedReason;
    private DateTime? _blockedUntil;
    private string? _lastError;
    private DateTime? _lastErrorAt;
    private DateTime? _refreshedAt;
    private DateTime? _lastUsedAt;
    private DateTime? _keyRejectedAt;
    private string? _keyRejection;
    private int _consecutiveFailures;

    /// <summary>The current position. Re-evaluated on each read, so a block lifts by itself.</summary>
    public AnthropicCreditStatus Snapshot(bool configured, decimal stopAt, DateTime now)
    {
        lock (_gate)
        {
            var status = new AnthropicCreditStatus
            {
                Grant = _grant,
                UsedUsd = Math.Round(_used, 6),
                StopAt = stopAt,
                BlockedReason = _blockedReason,
                BlockedUntil = _blockedUntil,
                LastError = _lastError,
                LastErrorAt = _lastErrorAt,
                RefreshedAt = _refreshedAt,
                LastUsedAt = _lastUsedAt
            };

            status.State = !configured ? AnthropicCreditState.Off
                : !_counted ? AnthropicCreditState.Unchecked
                : _grant is null || now < _grant.StartsAt || now >= _grant.ExpiresAt ? AnthropicCreditState.NoGrant
                : _blockedUntil is { } until && now < until ? AnthropicCreditState.Blocked
                : _used >= _grant.AmountUsd * stopAt ? AnthropicCreditState.Exhausted
                : AnthropicCreditState.Active;

            if (status.State != AnthropicCreditState.Blocked)
            {
                status.BlockedReason = null;
                status.BlockedUntil = null;
            }

            return status;
        }
    }

    /// <summary>Takes the grant and the count from the logs.</summary>
    public void Apply(CreditGrant? grant, decimal loggedUsd, DateTime now)
    {
        lock (_gate)
        {
            var sameCycle = grant is not null && _grant is not null && grant.Id == _grant.Id;

            // A new cycle starts from what the logs say. Within one, keep whichever is
            // higher: calls made since the last refresh may not be logged yet.
            _used = sameCycle ? Math.Max(_used, loggedUsd) : loggedUsd;

            // A grant edited down (or up) is the same cycle with a new amount; take it.
            _grant = grant;
            _counted = true;
            _refreshedAt = now;
        }
    }

    public void AddSpend(decimal usd, DateTime now)
    {
        if (usd <= 0) return;
        lock (_gate)
        {
            _used += usd;
            _lastUsedAt = now;
        }
    }

    /// <summary>A call got an answer: the API is up, so the failure run is over.</summary>
    public void RecordSuccess()
    {
        lock (_gate)
        {
            _consecutiveFailures = 0;
        }
    }

    /// <summary>
    /// Counts a failure that says nothing about the credit or the key -- a timeout, a
    /// 5xx, no connection -- and returns how many have happened in a row. The caller
    /// uses it to stop trying for a while, so an outage costs each request nothing
    /// rather than a timeout.
    /// </summary>
    public int RecordFailure(string reason, DateTime now)
    {
        lock (_gate)
        {
            _lastError = reason;
            _lastErrorAt = now;
            return ++_consecutiveFailures;
        }
    }

    public void Block(string reason, TimeSpan duration, DateTime now)
    {
        lock (_gate)
        {
            _blockedReason = reason;
            _blockedUntil = now + duration;
            _lastError = reason;
            _lastErrorAt = now;
        }
    }

    /// <summary>Notes a failure that does not stop the credit being tried next time.</summary>
    public void NoteError(string reason, DateTime now)
    {
        lock (_gate)
        {
            _lastError = reason;
            _lastErrorAt = now;
        }
    }

    /// <summary>
    /// The provider refused the key itself (401/403). Kept apart from the block so the
    /// key watch can tell the operator, which it does off the request path.
    /// </summary>
    public void RecordKeyRejection(string reason, DateTime now)
    {
        lock (_gate)
        {
            _keyRejectedAt = now;
            _keyRejection = reason;
        }
    }

    public (DateTime? At, string? Reason) KeyRejection
    {
        get
        {
            lock (_gate)
            {
                return (_keyRejectedAt, _keyRejection);
            }
        }
    }

    public void ClearBlock()
    {
        lock (_gate)
        {
            _blockedReason = null;
            _blockedUntil = null;
            _consecutiveFailures = 0;
        }
    }
}

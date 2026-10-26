using Freeway.Domain.Common;

namespace Freeway.Domain.Entities;

/// <summary>
/// A prepaid allowance that a lane may spend before falling back, such as the
/// monthly Claude API credit that comes with a Max plan.
///
/// The provider holds the real balance and offers no way to read it for an
/// individual account, so Freeway keeps its own: the grant is recorded here and
/// what has been spent against it is the sum of usage_logs.credit_cost_usd between
/// <see cref="StartsAt"/> and <see cref="ExpiresAt"/>.
/// </summary>
public class CreditGrant : BaseEntity
{
    /// <summary>Whose credit this is, e.g. "anthropic".</summary>
    public string Provider { get; set; } = string.Empty;

    public decimal AmountUsd { get; set; }

    public DateTime StartsAt { get; set; }

    /// <summary>Exclusive. Unused credit is gone at this point; it does not roll over.</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>"auto" when Freeway opened the cycle itself, "manual" once someone edits it.</summary>
    public string Source { get; set; } = "auto";

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

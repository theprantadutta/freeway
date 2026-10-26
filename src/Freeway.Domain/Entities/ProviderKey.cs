using Freeway.Domain.Common;

namespace Freeway.Domain.Entities;

/// <summary>
/// A provider API key Freeway has run with, known only by a fingerprint.
///
/// Exists because keys expire and the provider will not say when: the Claude API
/// gives a workspace key no way to read its own expiry. So the gateway notes the
/// first time it sees each key and works the expiry out from the key lifetime set
/// when keys are created, which can be corrected per key from the dashboard. A new
/// key -- a rotation -- is simply a fingerprint not seen before, so nothing has to be
/// updated by hand when one is swapped in.
///
/// The key itself is never stored: only a SHA-256 fingerprint and a short hint.
/// </summary>
public class ProviderKey : BaseEntity
{
    public string Provider { get; set; } = string.Empty;

    /// <summary>Hex SHA-256 of the key, truncated. Identifies it without revealing it.</summary>
    public string Fingerprint { get; set; } = string.Empty;

    /// <summary>The first and last few characters, e.g. "sk-ant-usr-1j…Xy9Q", for recognising it.</summary>
    public string Hint { get; set; } = string.Empty;

    public DateTime FirstSeenAt { get; set; }
    public DateTime LastSeenAt { get; set; }

    /// <summary>When the key stops working. Estimated from the lifetime until set by hand.</summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>"estimated" (first seen plus the configured lifetime) or "manual".</summary>
    public string ExpirySource { get; set; } = "estimated";

    /// <summary>The provider last refused the key, and why.</summary>
    public DateTime? LastRejectedAt { get; set; }
    public string? LastRejection { get; set; }

    // When each reminder went out, so each is sent once per key (rejections at most
    // twice a day) however many times the check runs or the API restarts.
    public DateTime? WarnedAt { get; set; }
    public DateTime? ExpiredNoticeAt { get; set; }
    public DateTime? RejectedNoticeAt { get; set; }
}

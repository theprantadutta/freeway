using Freeway.Domain.Common;

namespace Freeway.Domain.Entities;

public class Project : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string ApiKeyHash { get; set; } = string.Empty;
    public string ApiKeyPrefix { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
    public int RateLimitPerMinute { get; set; } = 60;
    public Dictionary<string, object>? Metadata { get; set; }

    public ICollection<UsageLog> UsageLogs { get; set; } = new List<UsageLog>();
}

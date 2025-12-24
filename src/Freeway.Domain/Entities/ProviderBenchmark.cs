using Freeway.Domain.Common;

namespace Freeway.Domain.Entities;

public class ProviderBenchmark : BaseEntity
{
    public string ProviderName { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public int ResponseTimeMs { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int? ErrorCode { get; set; }
    public DateTime TestedAt { get; set; }
}

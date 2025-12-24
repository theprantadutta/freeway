namespace Freeway.Domain.Interfaces;

public interface IProjectCacheService
{
    Task LoadCacheAsync(CancellationToken cancellationToken = default);
    ProjectInfo? ValidateApiKey(string apiKey);
    void InvalidateCache();
}

public class ProjectInfo
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int RateLimitPerMinute { get; set; }
    public bool IsActive { get; set; }
}

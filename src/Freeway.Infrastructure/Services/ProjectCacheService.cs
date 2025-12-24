using Freeway.Domain.Interfaces;
using Freeway.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Freeway.Infrastructure.Services;

public class ProjectCacheService : IProjectCacheService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IApiKeyService _apiKeyService;
    private readonly ILogger<ProjectCacheService> _logger;
    private readonly object _lock = new();

    private Dictionary<string, CachedProject> _projectsByHash = new();

    public ProjectCacheService(
        IServiceScopeFactory scopeFactory,
        IApiKeyService apiKeyService,
        ILogger<ProjectCacheService> logger)
    {
        _scopeFactory = scopeFactory;
        _apiKeyService = apiKeyService;
        _logger = logger;
    }

    public async Task LoadCacheAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Loading project cache from database...");

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var projects = await context.Projects
                .Where(p => p.IsActive)
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.ApiKeyHash,
                    p.RateLimitPerMinute,
                    p.IsActive
                })
                .ToListAsync(cancellationToken);

            var newCache = new Dictionary<string, CachedProject>();
            foreach (var project in projects)
            {
                newCache[project.ApiKeyHash] = new CachedProject
                {
                    Id = project.Id,
                    Name = project.Name,
                    ApiKeyHash = project.ApiKeyHash,
                    RateLimitPerMinute = project.RateLimitPerMinute,
                    IsActive = project.IsActive
                };
            }

            lock (_lock)
            {
                _projectsByHash = newCache;
            }

            _logger.LogInformation("Project cache loaded: {Count} active projects", projects.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load project cache");
        }
    }

    public ProjectInfo? ValidateApiKey(string apiKey)
    {
        if (string.IsNullOrEmpty(apiKey))
            return null;

        lock (_lock)
        {
            foreach (var (hash, project) in _projectsByHash)
            {
                if (_apiKeyService.VerifyApiKey(apiKey, hash))
                {
                    return new ProjectInfo
                    {
                        Id = project.Id,
                        Name = project.Name,
                        RateLimitPerMinute = project.RateLimitPerMinute,
                        IsActive = project.IsActive
                    };
                }
            }
        }

        return null;
    }

    public void InvalidateCache()
    {
        _logger.LogInformation("Invalidating project cache...");
        // Trigger async reload
        Task.Run(async () => await LoadCacheAsync());
    }

    private class CachedProject
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ApiKeyHash { get; set; } = string.Empty;
        public int RateLimitPerMinute { get; set; }
        public bool IsActive { get; set; }
    }
}

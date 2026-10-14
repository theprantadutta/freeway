using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
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

    // Replaced wholesale on every load and only ever read, so readers take a snapshot
    // of the reference instead of a lock.
    private volatile Snapshot _snapshot = new(new Dictionary<string, CachedProject>());

    // Keys already proven, by the SHA-256 of the key, so each one is BCrypt-verified
    // once rather than on every request. An entry only names a project; whether that
    // project still holds the key is checked against the current cache every time.
    private readonly ConcurrentDictionary<string, CachedProject> _verified = new();

    // Keys that matched no project are remembered per snapshot (see Snapshot), capped
    // so a flood of junk keys cannot grow the set without bound.
    private const int MaxRejected = 10_000;

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
                    p.ApiKeyPrefix,
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
                    ApiKeyPrefix = project.ApiKeyPrefix,
                    RateLimitPerMinute = project.RateLimitPerMinute,
                    IsActive = project.IsActive
                };
            }

            _snapshot = new Snapshot(newCache);

            // Entries whose project no longer holds the key are already refused (see
            // ValidateApiKey); dropping them here just keeps the memo from growing.
            foreach (var (keyDigest, project) in _verified)
            {
                if (!newCache.ContainsKey(project.ApiKeyHash))
                    _verified.TryRemove(keyDigest, out _);
            }

            _logger.LogInformation("Project cache loaded: {Count} active projects", projects.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load project cache");
        }
    }

    /// <summary>
    /// Finds the active project a key belongs to.
    ///
    /// This used to BCrypt-verify the key against every project in turn, inside a lock,
    /// on every request. BCrypt is slow on purpose, so the cost grew with the number of
    /// projects and concurrent requests queued behind one another -- measured at about
    /// 1.2s per request with seven projects, before any upstream call. It now verifies a
    /// given key once, trying the project whose stored prefix matches first, and
    /// remembers the answer.
    ///
    /// Revocation works exactly as before. Rotating, deleting or deactivating a project
    /// reloads this cache, and a remembered key is honoured only while its project is in
    /// the current cache under the same hash, so it stops working at the same moment it
    /// always did.
    /// </summary>
    public ProjectInfo? ValidateApiKey(string apiKey)
    {
        if (string.IsNullOrEmpty(apiKey))
            return null;

        var snapshot = _snapshot;
        var projects = snapshot.Projects;
        var digest = Digest(apiKey);

        if (_verified.TryGetValue(digest, out var known))
        {
            if (projects.TryGetValue(known.ApiKeyHash, out var current))
                return ToInfo(current);

            _verified.TryRemove(digest, out _);
        }

        if (snapshot.Rejected.ContainsKey(digest))
            return null;

        // The prefix is the key's first characters, stored at creation, so it narrows
        // the search to one project in practice. Every other project is still tried
        // afterwards, so a row whose prefix does not match its key still works.
        var prefixMatches = projects.Values.Where(p => MatchesPrefix(apiKey, p.ApiKeyPrefix)).ToList();
        var candidates = prefixMatches.Concat(projects.Values.Except(prefixMatches));

        foreach (var project in candidates)
        {
            if (_apiKeyService.VerifyApiKey(apiKey, project.ApiKeyHash))
            {
                _verified[digest] = project;
                return ToInfo(project);
            }
        }

        if (snapshot.Rejected.Count < MaxRejected)
            snapshot.Rejected.TryAdd(digest, 0);

        return null;
    }

    public void InvalidateCache()
    {
        _logger.LogInformation("Invalidating project cache...");
        // Trigger async reload
        Task.Run(async () => await LoadCacheAsync());
    }

    private static bool MatchesPrefix(string apiKey, string prefix) =>
        !string.IsNullOrEmpty(prefix) && apiKey.StartsWith(prefix, StringComparison.Ordinal);

    private static string Digest(string apiKey) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(apiKey)));

    private static ProjectInfo ToInfo(CachedProject project) => new()
    {
        Id = project.Id,
        Name = project.Name,
        RateLimitPerMinute = project.RateLimitPerMinute,
        IsActive = project.IsActive
    };

    /// <summary>
    /// The projects as of one load, with the keys found to match none of them.
    ///
    /// The rejections live with the projects they were checked against, so a reload
    /// starts with none. Were they shared, a request still reading the previous
    /// snapshot could record a just-created key as invalid after the reload had
    /// cleared the set, locking that key out until the next one.
    /// </summary>
    private sealed record Snapshot(Dictionary<string, CachedProject> Projects)
    {
        public ConcurrentDictionary<string, byte> Rejected { get; } = new();
    }

    private class CachedProject
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ApiKeyHash { get; set; } = string.Empty;
        public string ApiKeyPrefix { get; set; } = string.Empty;
        public int RateLimitPerMinute { get; set; }
        public bool IsActive { get; set; }
    }
}

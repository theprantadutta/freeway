using Freeway.Domain.Interfaces;
using Freeway.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Freeway.Infrastructure.Notifications;

public interface IUsageReportBuilder
{
    /// <summary>
    /// Builds the weekly report. <paramref name="weekEnd"/> defaults to now; the window
    /// is the seven days before the most recent Monday 00:00 UTC.
    /// </summary>
    Task<WeeklyUsageReport> BuildWeeklyAsync(DateTime? weekEnd = null, CancellationToken cancellationToken = default);
}

public class UsageReportBuilder : IUsageReportBuilder
{
    private readonly AppDbContext _context;
    private readonly IOpenRouterService _openRouterService;
    private readonly ILocalClaudeService _localClaude;
    private readonly ILogger<UsageReportBuilder> _logger;

    public UsageReportBuilder(
        AppDbContext context,
        IOpenRouterService openRouterService,
        ILocalClaudeService localClaude,
        ILogger<UsageReportBuilder> logger)
    {
        _context = context;
        _openRouterService = openRouterService;
        _localClaude = localClaude;
        _logger = logger;
    }

    public async Task<WeeklyUsageReport> BuildWeeklyAsync(
        DateTime? weekEnd = null,
        CancellationToken cancellationToken = default)
    {
        var now = weekEnd ?? DateTime.UtcNow;

        // Window is the completed week: the Monday before last through Sunday 23:59:59.
        var thisMonday = StartOfWeek(now);
        var periodStart = thisMonday.AddDays(-7);
        var periodEnd = thisMonday.AddTicks(-1);
        var previousStart = periodStart.AddDays(-7);
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var report = new WeeklyUsageReport
        {
            PeriodStart = periodStart,
            PeriodEnd = periodEnd
        };

        // Pull the window once and aggregate in memory. A week of logs is small, and
        // this keeps the per-project / per-model / per-tier cuts consistent.
        var weekLogs = await _context.UsageLogs
            .Where(u => u.CreatedAt >= periodStart && u.CreatedAt <= periodEnd)
            .Select(u => new LogSlice
            {
                ProjectId = u.ProjectId,
                ModelId = u.ModelId,
                ModelType = u.ModelType,
                ModelTier = u.ModelTier,
                CostSource = u.CostSource,
                AvoidedCostUsd = u.AvoidedCostUsd,
                InputTokens = u.InputTokens,
                OutputTokens = u.OutputTokens,
                CostUsd = u.CostUsd,
                ResponseTimeMs = u.ResponseTimeMs,
                Success = u.Success
            })
            .ToListAsync(cancellationToken);

        report.ThisWeek = Totals(weekLogs);

        report.PreviousWeek = await TotalsForRangeAsync(previousStart, periodStart.AddTicks(-1), cancellationToken);
        report.MonthToDate = await TotalsForRangeAsync(monthStart, now, cancellationToken);
        report.AllTime = await TotalsForRangeAsync(null, null, cancellationToken);

        var projectNames = await _context.Projects
            .Select(p => new { p.Id, p.Name, p.IsActive })
            .ToListAsync(cancellationToken);

        report.TotalProjects = projectNames.Count;
        report.ActiveProjects = projectNames.Count(p => p.IsActive);

        var nameById = projectNames.ToDictionary(p => p.Id, p => p.Name);

        report.Projects = weekLogs
            .GroupBy(l => l.ProjectId)
            .Select(g => new ProjectUsageLine
            {
                ProjectId = g.Key,
                ProjectName = nameById.TryGetValue(g.Key, out var name) ? name : "(deleted project)",
                Requests = g.Count(),
                FailedRequests = g.Count(l => !l.Success),
                Tokens = g.Sum(l => (long)l.InputTokens + l.OutputTokens),
                CostUsd = g.Sum(l => l.CostUsd)
            })
            .OrderByDescending(p => p.CostUsd)
            .ThenByDescending(p => p.Requests)
            .ToList();

        report.Models = weekLogs
            .GroupBy(l => new { l.ModelId, l.ModelType, l.ModelTier })
            .Select(g => new ModelUsageLine
            {
                ModelId = g.Key.ModelId,
                ModelType = g.Key.ModelType,
                ModelTier = g.Key.ModelTier,
                Requests = g.Count(),
                Tokens = g.Sum(l => (long)l.InputTokens + l.OutputTokens),
                CostUsd = g.Sum(l => l.CostUsd)
            })
            .OrderByDescending(m => m.Requests)
            .ToList();

        var localRows = weekLogs.Where(l => l.CostSource == "subscription").ToList();
        report.LocalClaudeRequests = localRows.Count;
        report.LocalClaudeAvoidedUsd = localRows.Sum(l => l.AvoidedCostUsd ?? 0m);
        report.LocalClaudeStatus = _localClaude.IsConfigured ? _localClaude.Health.Describe() : null;

        report.CostSources = weekLogs
            .GroupBy(l => l.CostSource ?? "legacy")
            .ToDictionary(g => g.Key, g => g.Count());

        report.Tiers = weekLogs
            .GroupBy(l => TierLabel(l.ModelType, l.ModelTier))
            .Select(g => new TierUsageLine
            {
                Tier = g.Key,
                Requests = g.Count(),
                CostUsd = g.Sum(l => l.CostUsd)
            })
            .OrderByDescending(t => t.CostUsd)
            .ToList();

        try
        {
            report.Credit = await _openRouterService.GetCreditsAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read OpenRouter credit for the weekly report");
        }

        return report;
    }

    /// <summary>Monday 00:00 UTC of the week containing <paramref name="value"/>.</summary>
    private static DateTime StartOfWeek(DateTime value)
    {
        var utc = DateTime.SpecifyKind(value, DateTimeKind.Utc).Date;
        var delta = (7 + (int)utc.DayOfWeek - (int)DayOfWeek.Monday) % 7;
        return utc.AddDays(-delta);
    }

    /// <summary>
    /// Maps a log row onto one of the lanes the panel shows. A row written before
    /// tiers existed has no tier and falls back to its type.
    /// </summary>
    internal static string TierLabel(string modelType, string? modelTier)
    {
        if (!string.IsNullOrWhiteSpace(modelTier)) return modelTier.ToLowerInvariant();

        return (modelType ?? "").ToLowerInvariant() switch
        {
            "free" => "free",
            "image" => "image",
            "paid" => "low",
            _ => "other"
        };
    }

    private async Task<UsageTotals> TotalsForRangeAsync(
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken)
    {
        var query = _context.UsageLogs.AsQueryable();
        if (from.HasValue) query = query.Where(u => u.CreatedAt >= from.Value);
        if (to.HasValue) query = query.Where(u => u.CreatedAt <= to.Value);

        var slices = await query
            .Select(u => new LogSlice
            {
                InputTokens = u.InputTokens,
                OutputTokens = u.OutputTokens,
                CostUsd = u.CostUsd,
                ResponseTimeMs = u.ResponseTimeMs,
                Success = u.Success
            })
            .ToListAsync(cancellationToken);

        return Totals(slices);
    }

    private static UsageTotals Totals(List<LogSlice> logs) => new()
    {
        Requests = logs.Count,
        FailedRequests = logs.Count(l => !l.Success),
        InputTokens = logs.Sum(l => (long)l.InputTokens),
        OutputTokens = logs.Sum(l => (long)l.OutputTokens),
        CostUsd = logs.Sum(l => l.CostUsd),
        AvgResponseTimeMs = logs.Count == 0 ? 0 : logs.Average(l => l.ResponseTimeMs)
    };

    private class LogSlice
    {
        public Guid ProjectId { get; set; }
        public string ModelId { get; set; } = string.Empty;
        public string ModelType { get; set; } = string.Empty;
        public string? ModelTier { get; set; }
        public string? CostSource { get; set; }
        public decimal? AvoidedCostUsd { get; set; }
        public int InputTokens { get; set; }
        public int OutputTokens { get; set; }
        public decimal CostUsd { get; set; }
        public int ResponseTimeMs { get; set; }
        public bool Success { get; set; }
    }
}

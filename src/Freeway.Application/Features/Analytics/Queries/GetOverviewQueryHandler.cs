using Freeway.Application.Common;
using Freeway.Application.DTOs;
using Freeway.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Freeway.Application.Features.Analytics.Queries;

public class GetOverviewQueryHandler : IRequestHandler<GetOverviewQuery, Result<OverviewDto>>
{
    private const int MaxDays = 180;
    private const int MaxRecent = 25;
    private const int MaxLeaders = 10;
    private const int MaxErrorLength = 200;

    private readonly IAppDbContext _context;
    private readonly IDateTimeService _dateTimeService;

    public GetOverviewQueryHandler(IAppDbContext context, IDateTimeService dateTimeService)
    {
        _context = context;
        _dateTimeService = dateTimeService;
    }

    public async Task<Result<OverviewDto>> Handle(GetOverviewQuery request, CancellationToken cancellationToken)
    {
        var days = Math.Clamp(request.Days, 1, MaxDays);
        var projectId = request.ProjectId;

        var now = _dateTimeService.UtcNow;
        var todayStart = now.Date;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var previousMonthStart = monthStart.AddMonths(-1);
        var seriesStart = todayStart.AddDays(-(days - 1));
        var previousWindowStart = seriesStart.AddDays(-days);

        var projects = await _context.Projects
            .Select(p => new { p.Id, p.Name, p.IsActive })
            .ToListAsync(cancellationToken);

        // One pass over the window feeds the series, the lanes and the leaders. A few
        // months of logs is small, and reading it once keeps every cut on this page
        // consistent with the others.
        var windowStart = new[] { seriesStart, previousMonthStart, previousWindowStart }.Min();

        var logs = _context.UsageLogs.AsQueryable();
        if (projectId is { } only)
        {
            logs = logs.Where(u => u.ProjectId == only);
        }

        var windowLogs = await logs
            .Where(u => u.CreatedAt >= windowStart)
            .Select(u => new Slice
            {
                ProjectId = u.ProjectId,
                ModelId = u.ModelId,
                ModelType = u.ModelType,
                ModelTier = u.ModelTier,
                CreatedAt = u.CreatedAt,
                CostUsd = u.CostUsd,
                InputTokens = u.InputTokens,
                OutputTokens = u.OutputTokens,
                ResponseTimeMs = u.ResponseTimeMs,
                Success = u.Success,
                Provider = u.Provider,
                CostSource = u.CostSource,
                CreditCostUsd = u.CreditCostUsd,
                AvoidedCostUsd = u.AvoidedCostUsd,
                Streamed = u.Streamed
            })
            .ToListAsync(cancellationToken);

        foreach (var log in windowLogs)
        {
            log.Lane = LaneOf(log.ModelType, log.ModelTier);
            log.Backend = BackendOf(log.Provider, log.ModelType, log.CostSource);
        }

        var seriesLogs = windowLogs.Where(l => l.CreatedAt >= seriesStart).ToList();
        var previousWindowLogs = windowLogs
            .Where(l => l.CreatedAt >= previousWindowStart && l.CreatedAt < seriesStart)
            .ToList();
        var monthLogs = windowLogs.Where(l => l.CreatedAt >= monthStart).ToList();
        var previousMonthLogs = windowLogs
            .Where(l => l.CreatedAt >= previousMonthStart && l.CreatedAt < monthStart)
            .ToList();

        // All-time figures stay as aggregates: the table is far too large to pull.
        var requestsAllTime = await logs.CountAsync(cancellationToken);
        var costAllTime = await logs.SumAsync(u => (decimal?)u.CostUsd, cancellationToken) ?? 0m;

        var dto = new OverviewDto
        {
            Days = days,
            ProjectId = projectId,
            Totals = new OverviewTotalsDto
            {
                TotalProjects = projects.Count,
                ActiveProjects = projects.Count(p => p.IsActive),

                RequestsToday = windowLogs.Count(l => l.CreatedAt >= todayStart),
                RequestsThisMonth = monthLogs.Count,
                RequestsAllTime = requestsAllTime,
                RequestsPreviousMonth = previousMonthLogs.Count,

                CostToday = windowLogs.Where(l => l.CreatedAt >= todayStart).Sum(l => l.CostUsd),
                CostThisMonth = monthLogs.Sum(l => l.CostUsd),
                CostPreviousMonth = previousMonthLogs.Sum(l => l.CostUsd),
                CostAllTime = costAllTime,

                TokensThisMonth = monthLogs.Sum(l => (long)l.InputTokens + l.OutputTokens),
                FailuresThisMonth = monthLogs.Count(l => !l.Success),
                SuccessRateThisMonth = monthLogs.Count == 0
                    ? 100
                    : (double)monthLogs.Count(l => l.Success) / monthLogs.Count * 100,
                AvgResponseMsThisMonth = monthLogs.Count == 0 ? 0 : monthLogs.Average(l => l.ResponseTimeMs),
                CreditThisMonth = monthLogs.Sum(l => l.CreditCostUsd ?? 0m),
                AvoidedThisMonth = monthLogs.Sum(l => l.AvoidedCostUsd ?? 0m)
            },
            Window = Totals(seriesLogs, seriesStart, now),
            PreviousWindow = Totals(previousWindowLogs, previousWindowStart, seriesStart)
        };

        // Every day in the span gets a point, including the empty ones: gaps in a chart
        // should read as "nothing happened", not as missing data.
        var byDay = seriesLogs
            .GroupBy(l => l.CreatedAt.Date)
            .ToDictionary(g => g.Key, g => g.ToList());

        var dayIndex = new Dictionary<DateTime, int>();
        for (var day = seriesStart; day <= todayStart; day = day.AddDays(1))
        {
            dayIndex[day] = dto.Series.Count;
            byDay.TryGetValue(day, out var entries);
            entries ??= [];

            dto.Series.Add(new UsagePointDto
            {
                Date = day,
                Requests = entries.Count,
                Failures = entries.Count(l => !l.Success),
                Cost = entries.Sum(l => l.CostUsd),
                Tokens = entries.Sum(l => (long)l.InputTokens + l.OutputTokens),
                CreditUsd = entries.Sum(l => l.CreditCostUsd ?? 0m),
                AvoidedUsd = entries.Sum(l => l.AvoidedCostUsd ?? 0m),
                Lanes = entries.GroupBy(l => l.Lane).ToDictionary(g => g.Key, g => g.Count()),
                Backends = entries.GroupBy(l => l.Backend).ToDictionary(g => g.Key, g => g.Count())
            });
        }

        dto.Lanes = seriesLogs
            .GroupBy(l => l.Lane)
            .Select(g => new LaneUsageDto
            {
                Lane = g.Key,
                Requests = g.Count(),
                Cost = g.Sum(l => l.CostUsd),
                Tokens = g.Sum(l => (long)l.InputTokens + l.OutputTokens)
            })
            .OrderByDescending(l => l.Requests)
            .ToList();

        dto.Backends = seriesLogs
            .GroupBy(l => l.Backend)
            .Select(g => new BackendUsageDto
            {
                Backend = g.Key,
                Requests = g.Count(),
                Failures = g.Count(l => !l.Success),
                Tokens = g.Sum(l => (long)l.InputTokens + l.OutputTokens),
                Cost = g.Sum(l => l.CostUsd),
                CreditUsd = g.Sum(l => l.CreditCostUsd ?? 0m),
                AvoidedUsd = g.Sum(l => l.AvoidedCostUsd ?? 0m),
                AvgResponseMs = g.Average(l => l.ResponseTimeMs)
            })
            .OrderByDescending(b => b.Requests)
            .ToList();

        dto.TopModels = seriesLogs
            .GroupBy(l => new { l.ModelId, l.ModelType, l.ModelTier })
            .Select(g => new TopModelDto
            {
                ModelId = g.Key.ModelId,
                ModelType = g.Key.ModelType,
                ModelTier = g.Key.ModelTier,
                Requests = g.Count(),
                Cost = g.Sum(l => l.CostUsd),
                Tokens = g.Sum(l => (long)l.InputTokens + l.OutputTokens),
                Failures = g.Count(l => !l.Success),
                AvgResponseMs = g.Average(l => l.ResponseTimeMs),
                CreditUsd = g.Sum(l => l.CreditCostUsd ?? 0m),
                Backend = g.GroupBy(l => l.Backend).OrderByDescending(b => b.Count()).First().Key
            })
            .OrderByDescending(m => m.Requests)
            .Take(MaxLeaders)
            .ToList();

        var nameById = projects.ToDictionary(p => p.Id, p => p);
        string NameOf(Guid id) => nameById.TryGetValue(id, out var p) ? p.Name : "(deleted project)";

        dto.TopProjects = seriesLogs
            .GroupBy(l => l.ProjectId)
            .Select(g =>
            {
                var daily = new int[dto.Series.Count];
                foreach (var l in g)
                {
                    if (dayIndex.TryGetValue(l.CreatedAt.Date, out var i)) daily[i]++;
                }

                return new TopProjectDto
                {
                    ProjectId = g.Key,
                    Name = NameOf(g.Key),
                    IsActive = nameById.TryGetValue(g.Key, out var pa) && pa.IsActive,
                    Requests = g.Count(),
                    Cost = g.Sum(l => l.CostUsd),
                    Tokens = g.Sum(l => (long)l.InputTokens + l.OutputTokens),
                    Failures = g.Count(l => !l.Success),
                    CreditUsd = g.Sum(l => l.CreditCostUsd ?? 0m),
                    AvgResponseMs = g.Average(l => l.ResponseTimeMs),
                    TopLane = g.GroupBy(l => l.Lane).OrderByDescending(x => x.Count()).First().Key,
                    Daily = daily.ToList()
                };
            })
            .OrderByDescending(p => p.Requests)
            .Take(MaxLeaders)
            .ToList();

        dto.Flows = seriesLogs
            .GroupBy(l => new { l.ProjectId, l.Lane, l.Backend })
            .Select(g => new FlowDto
            {
                ProjectId = g.Key.ProjectId,
                ProjectName = NameOf(g.Key.ProjectId),
                Lane = g.Key.Lane,
                Backend = g.Key.Backend,
                Requests = g.Count(),
                Tokens = g.Sum(l => (long)l.InputTokens + l.OutputTokens),
                Cost = g.Sum(l => l.CostUsd)
            })
            .OrderByDescending(f => f.Requests)
            .ToList();

        var recent = await logs
            .OrderByDescending(u => u.CreatedAt)
            .Take(MaxRecent)
            .Select(u => new RecentRequestDto
            {
                Id = u.Id,
                ProjectId = u.ProjectId,
                ProjectName = u.Project.Name,
                ModelId = u.ModelId,
                ModelType = u.ModelType,
                ModelTier = u.ModelTier,
                Success = u.Success,
                ResponseTimeMs = u.ResponseTimeMs,
                CostUsd = u.CostUsd,
                TotalTokens = u.InputTokens + u.OutputTokens,
                InputTokens = u.InputTokens,
                OutputTokens = u.OutputTokens,
                CreatedAt = u.CreatedAt,
                Provider = u.Provider,
                CostSource = u.CostSource,
                CreditCostUsd = u.CreditCostUsd,
                AvoidedCostUsd = u.AvoidedCostUsd,
                Streamed = u.Streamed,
                ErrorMessage = u.ErrorMessage
            })
            .ToListAsync(cancellationToken);

        foreach (var r in recent)
        {
            r.Backend = BackendOf(r.Provider, r.ModelType, r.CostSource);
            if (r.ErrorMessage is { Length: > MaxErrorLength } long_)
            {
                r.ErrorMessage = long_[..MaxErrorLength] + "…";
            }
        }

        dto.Recent = recent;

        return Result<OverviewDto>.Success(dto);
    }

    private static WindowTotalsDto Totals(List<Slice> logs, DateTime from, DateTime to)
    {
        var times = logs.Where(l => l.Success).Select(l => l.ResponseTimeMs).OrderBy(t => t).ToList();

        return new WindowTotalsDto
        {
            From = from,
            To = to,
            Requests = logs.Count,
            Failures = logs.Count(l => !l.Success),
            Tokens = logs.Sum(l => (long)l.InputTokens + l.OutputTokens),
            Cost = logs.Sum(l => l.CostUsd),
            CreditUsd = logs.Sum(l => l.CreditCostUsd ?? 0m),
            AvoidedUsd = logs.Sum(l => l.AvoidedCostUsd ?? 0m),
            Unbilled = logs.Count(l => l.Success && l.Backend != "openrouter"),
            Streamed = logs.Count(l => l.Streamed),
            AvgResponseMs = logs.Count == 0 ? 0 : logs.Average(l => l.ResponseTimeMs),
            P50ResponseMs = Percentile(times, 0.50),
            P95ResponseMs = Percentile(times, 0.95)
        };
    }

    private static double Percentile(List<int> sorted, double p)
    {
        if (sorted.Count == 0) return 0;
        var rank = p * (sorted.Count - 1);
        var lower = (int)Math.Floor(rank);
        var upper = (int)Math.Ceiling(rank);
        return sorted[lower] + (sorted[upper] - sorted[lower]) * (rank - lower);
    }

    /// <summary>
    /// Maps a row onto one of the lanes the panel shows. Rows written before tiers
    /// existed carry no tier and fall back to their type.
    /// </summary>
    private static string LaneOf(string modelType, string? modelTier)
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

    /// <summary>
    /// Who answered a request, as the panel groups it. The Claude API credit and the
    /// Claude Code subscription cost nothing per request; free-lane providers are free
    /// by definition; everything else went to a paid provider (OpenRouter).
    /// </summary>
    private static string BackendOf(string? provider, string modelType, string? costSource) =>
        provider switch
        {
            "anthropic" => "credit",
            "claude-code" => "bridge",
            _ when string.Equals(modelType, "free", StringComparison.OrdinalIgnoreCase) || costSource == "free_tier" => "free",
            "openrouter" => "openrouter",
            null or "" => "other",
            _ => "openrouter"
        };

    private class Slice
    {
        public Guid ProjectId { get; set; }
        public string ModelId { get; set; } = string.Empty;
        public string ModelType { get; set; } = string.Empty;
        public string? ModelTier { get; set; }
        public DateTime CreatedAt { get; set; }
        public decimal CostUsd { get; set; }
        public int InputTokens { get; set; }
        public int OutputTokens { get; set; }
        public int ResponseTimeMs { get; set; }
        public bool Success { get; set; }
        public string? Provider { get; set; }
        public string? CostSource { get; set; }
        public decimal? CreditCostUsd { get; set; }
        public decimal? AvoidedCostUsd { get; set; }
        public bool Streamed { get; set; }
        public string Lane { get; set; } = string.Empty;
        public string Backend { get; set; } = string.Empty;
    }
}

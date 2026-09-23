using Freeway.Application.Common;
using Freeway.Application.DTOs;
using Freeway.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Freeway.Application.Features.Analytics.Queries;

public class GetOverviewQueryHandler : IRequestHandler<GetOverviewQuery, Result<OverviewDto>>
{
    private const int MaxDays = 180;
    private const int MaxRecent = 12;
    private const int MaxLeaders = 6;

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

        var now = _dateTimeService.UtcNow;
        var todayStart = now.Date;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var previousMonthStart = monthStart.AddMonths(-1);
        var seriesStart = todayStart.AddDays(-(days - 1));

        var projects = await _context.Projects
            .Select(p => new { p.Id, p.Name, p.IsActive })
            .ToListAsync(cancellationToken);

        // One pass over the window feeds the series, the lanes and the leaders. A month
        // of logs is small, and reading it once keeps every cut on this page consistent
        // with the others.
        var windowStart = seriesStart < previousMonthStart ? seriesStart : previousMonthStart;

        var windowLogs = await _context.UsageLogs
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
                Success = u.Success
            })
            .ToListAsync(cancellationToken);

        var seriesLogs = windowLogs.Where(l => l.CreatedAt >= seriesStart).ToList();
        var monthLogs = windowLogs.Where(l => l.CreatedAt >= monthStart).ToList();
        var previousMonthLogs = windowLogs
            .Where(l => l.CreatedAt >= previousMonthStart && l.CreatedAt < monthStart)
            .ToList();

        // All-time figures stay as aggregates: the table is far too large to pull.
        var requestsAllTime = await _context.UsageLogs.CountAsync(cancellationToken);
        var costAllTime = await _context.UsageLogs
            .SumAsync(u => (decimal?)u.CostUsd, cancellationToken) ?? 0m;

        var dto = new OverviewDto
        {
            Days = days,
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
                AvgResponseMsThisMonth = monthLogs.Count == 0 ? 0 : monthLogs.Average(l => l.ResponseTimeMs)
            }
        };

        // Every day in the span gets a point, including the empty ones: gaps in a chart
        // should read as "nothing happened", not as missing data.
        var byDay = seriesLogs
            .GroupBy(l => l.CreatedAt.Date)
            .ToDictionary(g => g.Key, g => g.ToList());

        for (var day = seriesStart; day <= todayStart; day = day.AddDays(1))
        {
            byDay.TryGetValue(day, out var entries);
            dto.Series.Add(new UsagePointDto
            {
                Date = day,
                Requests = entries?.Count ?? 0,
                Failures = entries?.Count(l => !l.Success) ?? 0,
                Cost = entries?.Sum(l => l.CostUsd) ?? 0m,
                Tokens = entries?.Sum(l => (long)l.InputTokens + l.OutputTokens) ?? 0L
            });
        }

        dto.Lanes = seriesLogs
            .GroupBy(l => LaneOf(l.ModelType, l.ModelTier))
            .Select(g => new LaneUsageDto
            {
                Lane = g.Key,
                Requests = g.Count(),
                Cost = g.Sum(l => l.CostUsd),
                Tokens = g.Sum(l => (long)l.InputTokens + l.OutputTokens)
            })
            .OrderByDescending(l => l.Requests)
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
                Tokens = g.Sum(l => (long)l.InputTokens + l.OutputTokens)
            })
            .OrderByDescending(m => m.Requests)
            .Take(MaxLeaders)
            .ToList();

        var nameById = projects.ToDictionary(p => p.Id, p => p);

        dto.TopProjects = seriesLogs
            .GroupBy(l => l.ProjectId)
            .Select(g => new TopProjectDto
            {
                ProjectId = g.Key,
                Name = nameById.TryGetValue(g.Key, out var p) ? p.Name : "(deleted project)",
                IsActive = nameById.TryGetValue(g.Key, out var pa) && pa.IsActive,
                Requests = g.Count(),
                Cost = g.Sum(l => l.CostUsd)
            })
            .OrderByDescending(p => p.Requests)
            .Take(MaxLeaders)
            .ToList();

        dto.Recent = await _context.UsageLogs
            .OrderByDescending(u => u.CreatedAt)
            .Take(MaxRecent)
            .Select(u => new RecentRequestDto
            {
                Id = u.Id,
                ProjectName = u.Project.Name,
                ModelId = u.ModelId,
                ModelType = u.ModelType,
                ModelTier = u.ModelTier,
                Success = u.Success,
                ResponseTimeMs = u.ResponseTimeMs,
                CostUsd = u.CostUsd,
                TotalTokens = u.InputTokens + u.OutputTokens,
                CreatedAt = u.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return Result<OverviewDto>.Success(dto);
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
    }
}

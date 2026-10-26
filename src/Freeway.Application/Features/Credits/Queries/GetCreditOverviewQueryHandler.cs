using Freeway.Application.Common;
using Freeway.Application.DTOs;
using Freeway.Domain.Common;
using Freeway.Domain.Entities;
using Freeway.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Freeway.Application.Features.Credits.Queries;

public class GetCreditOverviewQueryHandler : IRequestHandler<GetCreditOverviewQuery, Result<CreditOverviewDto>>
{
    private const string Provider = "anthropic";
    private const int MaxRecent = 25;
    private const int MaxHistory = 12;

    private readonly IAppDbContext _context;
    private readonly IAnthropicCreditService _credit;
    private readonly IDateTimeService _dateTimeService;

    public GetCreditOverviewQueryHandler(
        IAppDbContext context,
        IAnthropicCreditService credit,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _credit = credit;
        _dateTimeService = dateTimeService;
    }

    public async Task<Result<CreditOverviewDto>> Handle(GetCreditOverviewQuery request, CancellationToken cancellationToken)
    {
        var now = _dateTimeService.UtcNow;

        // Recount first, so the page shows the logs' figure rather than one up to five
        // minutes old. It also opens a new cycle if the old one has just expired.
        var status = _credit.IsConfigured
            ? await _credit.RefreshAsync(cancellationToken)
            : _credit.Status;

        var dto = new CreditOverviewDto
        {
            Configured = _credit.IsConfigured,
            CanServe = status.CanServe,
            State = Slug(status.State),
            Summary = status.Describe(),
            BlockedReason = status.BlockedReason,
            BlockedUntil = status.BlockedUntil,
            LastError = status.LastError,
            LastErrorAt = status.LastErrorAt,
            RefreshedAt = status.RefreshedAt,
            LastUsedAt = status.LastUsedAt,
            StopAtPercent = Math.Round(status.StopAt * 100, 1),
            Lanes = PaidTierExtensions.All
                .Where(_credit.Serves)
                .Reverse()
                .Select(t => new CreditLaneDto { Tier = t.ToSlug(), Model = _credit.ModelFor(t) })
                .ToList()
        };

        if (_credit.KeyFingerprint is { } fingerprint)
        {
            var key = await _context.ProviderKeys
                .FirstOrDefaultAsync(k => k.Provider == Provider && k.Fingerprint == fingerprint, cancellationToken);
            if (key is not null)
            {
                dto.Key = new CreditKeyDto
                {
                    Hint = key.Hint,
                    Fingerprint = key.Fingerprint,
                    FirstSeenAt = key.FirstSeenAt,
                    LastSeenAt = key.LastSeenAt,
                    ExpiresAt = key.ExpiresAt,
                    ExpirySource = key.ExpirySource,
                    DaysLeft = key.ExpiresAt is { } e ? Math.Round((e - now).TotalDays, 1) : null,
                    LastRejectedAt = key.LastRejectedAt,
                    LastRejection = key.LastRejection,
                    WarnedAt = key.WarnedAt,
                    ExpiredNoticeAt = key.ExpiredNoticeAt
                };
            }
        }

        var grants = await _context.CreditGrants
            .Where(g => g.Provider == Provider)
            .OrderByDescending(g => g.StartsAt)
            .Take(MaxHistory)
            .ToListAsync(cancellationToken);

        var grant = status.Grant ?? grants.FirstOrDefault(g => g.StartsAt <= now && g.ExpiresAt > now);

        // History: every recorded cycle with what it ended up spending.
        if (grants.Count > 0)
        {
            var oldest = grants.Min(g => g.StartsAt);
            var spend = await _context.UsageLogs
                .Where(l => l.Provider == Provider && l.CreditCostUsd != null && l.CreatedAt >= oldest)
                .Select(l => new { l.CreatedAt, Cost = l.CreditCostUsd ?? 0m })
                .ToListAsync(cancellationToken);

            dto.History = grants.Select(g =>
            {
                var inCycle = spend.Where(s => s.CreatedAt >= g.StartsAt && s.CreatedAt < g.ExpiresAt).ToList();
                var item = ToDto(g);
                item.UsedUsd = Math.Round(inCycle.Sum(s => s.Cost), 6);
                item.Requests = inCycle.Count;
                return item;
            }).ToList();
        }

        if (grant is null)
        {
            return Result<CreditOverviewDto>.Success(dto);
        }

        dto.Grant = ToDto(grant);
        dto.Grant.UsedUsd = Math.Round(status.UsedUsd, 6);
        dto.UsedUsd = Math.Round(status.UsedUsd, 6);
        dto.RemainingUsd = Math.Round(Math.Max(0, grant.AmountUsd - status.UsedUsd), 6);
        dto.UsableUsd = Math.Round(grant.AmountUsd * status.StopAt, 2);
        dto.UsedPercent = grant.AmountUsd == 0 ? 0 : Math.Round(status.UsedUsd / grant.AmountUsd * 100, 2);

        var total = (grant.ExpiresAt - grant.StartsAt).TotalDays;
        var elapsed = Math.Clamp((now - grant.StartsAt).TotalDays, 0, total);
        dto.DaysTotal = Math.Round(total, 2);
        dto.DaysElapsed = Math.Round(elapsed, 2);
        dto.DaysLeft = Math.Round(total - elapsed, 2);

        // A rate needs at least a few hours of history to mean anything.
        var basis = Math.Max(elapsed, 0.25);
        dto.BurnPerDayUsd = Math.Round(status.UsedUsd / (decimal)basis, 6);
        dto.ProjectedUsd = Math.Round(Math.Min(grant.AmountUsd, status.UsedUsd + dto.BurnPerDayUsd * (decimal)dto.DaysLeft), 2);
        dto.ProjectedUnusedUsd = Math.Round(Math.Max(0, grant.AmountUsd - dto.ProjectedUsd), 2);

        var rows = await _context.UsageLogs
            .Where(l => l.Provider == Provider && l.CreditCostUsd != null &&
                        l.CreatedAt >= grant.StartsAt && l.CreatedAt < grant.ExpiresAt)
            .Select(l => new Row
            {
                Id = l.Id,
                ProjectId = l.ProjectId,
                ModelId = l.ModelId,
                ModelTier = l.ModelTier,
                CreatedAt = l.CreatedAt,
                Credit = l.CreditCostUsd ?? 0m,
                Avoided = l.AvoidedCostUsd,
                InputTokens = l.InputTokens,
                OutputTokens = l.OutputTokens,
                ResponseTimeMs = l.ResponseTimeMs,
                Streamed = l.Streamed,
                Success = l.Success
            })
            .ToListAsync(cancellationToken);

        var projects = await _context.Projects
            .Select(p => new { p.Id, p.Name })
            .ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken);

        dto.Requests = rows.Count;
        dto.Grant.Requests = rows.Count;
        dto.FailedRequests = rows.Count(r => !r.Success);
        dto.InputTokens = rows.Sum(r => (long)r.InputTokens);
        dto.OutputTokens = rows.Sum(r => (long)r.OutputTokens);
        dto.AvoidedUsd = Math.Round(rows.Sum(r => r.Avoided ?? 0m), 6);

        // Every day of the cycle up to today, including the quiet ones, so the chart
        // reads as a timeline rather than a list of busy days.
        var lastDay = (now < grant.ExpiresAt ? now : grant.ExpiresAt.AddTicks(-1)).Date;
        var byDay = rows.GroupBy(r => r.CreatedAt.Date).ToDictionary(g => g.Key, g => g.ToList());
        for (var day = grant.StartsAt.Date; day <= lastDay; day = day.AddDays(1))
        {
            byDay.TryGetValue(day, out var list);
            dto.Series.Add(new CreditDayDto
            {
                Date = day.ToString("yyyy-MM-dd"),
                CreditUsd = Math.Round(list?.Sum(r => r.Credit) ?? 0m, 6),
                Requests = list?.Count ?? 0,
                Tokens = list?.Sum(r => (long)r.InputTokens + r.OutputTokens) ?? 0
            });
        }

        dto.ByProject = rows
            .GroupBy(r => r.ProjectId)
            .Select(g => new CreditBreakdownDto
            {
                Key = g.Key.ToString(),
                Name = projects.GetValueOrDefault(g.Key, "Unknown project"),
                Requests = g.Count(),
                CreditUsd = Math.Round(g.Sum(r => r.Credit), 6),
                Tokens = g.Sum(r => (long)r.InputTokens + r.OutputTokens)
            })
            .OrderByDescending(b => b.CreditUsd)
            .ToList();

        dto.ByModel = rows
            .GroupBy(r => new { r.ModelId, r.ModelTier })
            .Select(g => new CreditBreakdownDto
            {
                Key = g.Key.ModelId,
                Name = g.Key.ModelId,
                Tier = g.Key.ModelTier,
                Requests = g.Count(),
                CreditUsd = Math.Round(g.Sum(r => r.Credit), 6),
                Tokens = g.Sum(r => (long)r.InputTokens + r.OutputTokens)
            })
            .OrderByDescending(b => b.CreditUsd)
            .ToList();

        dto.Recent = rows
            .OrderByDescending(r => r.CreatedAt)
            .Take(MaxRecent)
            .Select(r => new CreditRequestDto
            {
                Id = r.Id,
                CreatedAt = r.CreatedAt,
                ProjectName = projects.GetValueOrDefault(r.ProjectId, "Unknown project"),
                ModelId = r.ModelId,
                ModelTier = r.ModelTier,
                InputTokens = r.InputTokens,
                OutputTokens = r.OutputTokens,
                CreditUsd = Math.Round(r.Credit, 6),
                AvoidedUsd = r.Avoided is { } a ? Math.Round(a, 6) : null,
                ResponseTimeMs = r.ResponseTimeMs,
                Streamed = r.Streamed,
                Success = r.Success
            })
            .ToList();

        return Result<CreditOverviewDto>.Success(dto);
    }

    private static CreditGrantDto ToDto(CreditGrant g) => new()
    {
        Id = g.Id,
        AmountUsd = g.AmountUsd,
        StartsAt = g.StartsAt,
        ExpiresAt = g.ExpiresAt,
        Source = g.Source,
        Note = g.Note
    };

    private static string Slug(AnthropicCreditState state) => state switch
    {
        AnthropicCreditState.NoGrant => "no_grant",
        _ => state.ToString().ToLowerInvariant()
    };

    private sealed class Row
    {
        public Guid Id { get; init; }
        public Guid ProjectId { get; init; }
        public string ModelId { get; init; } = string.Empty;
        public string? ModelTier { get; init; }
        public DateTime CreatedAt { get; init; }
        public decimal Credit { get; init; }
        public decimal? Avoided { get; init; }
        public int InputTokens { get; init; }
        public int OutputTokens { get; init; }
        public int ResponseTimeMs { get; init; }
        public bool Streamed { get; init; }
        public bool Success { get; init; }
    }
}

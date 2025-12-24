using Freeway.Application.Common;
using Freeway.Application.DTOs;
using Freeway.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Freeway.Application.Features.Analytics.Queries;

public class GetProjectUsageQueryHandler : IRequestHandler<GetProjectUsageQuery, Result<ProjectUsageDto>>
{
    private readonly IAppDbContext _context;

    public GetProjectUsageQueryHandler(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<Result<ProjectUsageDto>> Handle(GetProjectUsageQuery request, CancellationToken cancellationToken)
    {
        var project = await _context.Projects
            .FirstOrDefaultAsync(p => p.Id == request.ProjectId, cancellationToken);

        if (project == null)
        {
            return Result<ProjectUsageDto>.NotFound("Project not found");
        }

        var query = _context.UsageLogs.Where(u => u.ProjectId == request.ProjectId);

        if (request.StartDate.HasValue)
            query = query.Where(u => u.CreatedAt >= request.StartDate.Value);

        if (request.EndDate.HasValue)
            query = query.Where(u => u.CreatedAt <= request.EndDate.Value);

        var logs = await query.ToListAsync(cancellationToken);

        var summary = new UsageSummaryDto
        {
            TotalRequests = logs.Count,
            SuccessfulRequests = logs.Count(l => l.Success),
            FailedRequests = logs.Count(l => !l.Success),
            TotalInputTokens = logs.Sum(l => l.InputTokens),
            TotalOutputTokens = logs.Sum(l => l.OutputTokens),
            TotalCostUsd = logs.Sum(l => l.CostUsd),
            AvgResponseTimeMs = logs.Count > 0 ? logs.Average(l => l.ResponseTimeMs) : 0
        };

        var byModel = logs
            .GroupBy(l => new { l.ModelId, l.ModelType })
            .Select(g => new ModelUsageStatsDto
            {
                ModelId = g.Key.ModelId,
                ModelType = g.Key.ModelType,
                Requests = g.Count(),
                Tokens = g.Sum(l => l.InputTokens + l.OutputTokens),
                CostUsd = g.Sum(l => l.CostUsd)
            })
            .OrderByDescending(m => m.Requests)
            .ToList();

        return Result<ProjectUsageDto>.Success(new ProjectUsageDto
        {
            ProjectId = project.Id,
            ProjectName = project.Name,
            Period = new PeriodDto
            {
                Start = request.StartDate,
                End = request.EndDate
            },
            Summary = summary,
            ByModel = byModel
        });
    }
}

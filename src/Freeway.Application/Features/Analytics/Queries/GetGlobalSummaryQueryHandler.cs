using Freeway.Application.Common;
using Freeway.Application.DTOs;
using Freeway.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Freeway.Application.Features.Analytics.Queries;

public class GetGlobalSummaryQueryHandler : IRequestHandler<GetGlobalSummaryQuery, Result<GlobalSummaryDto>>
{
    private readonly IAppDbContext _context;
    private readonly IDateTimeService _dateTimeService;

    public GetGlobalSummaryQueryHandler(IAppDbContext context, IDateTimeService dateTimeService)
    {
        _context = context;
        _dateTimeService = dateTimeService;
    }

    public async Task<Result<GlobalSummaryDto>> Handle(GetGlobalSummaryQuery request, CancellationToken cancellationToken)
    {
        var now = _dateTimeService.UtcNow;
        var todayStart = now.Date;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var totalProjects = await _context.Projects.CountAsync(cancellationToken);
        var activeProjects = await _context.Projects.CountAsync(p => p.IsActive, cancellationToken);

        var requestsToday = await _context.UsageLogs
            .CountAsync(u => u.CreatedAt >= todayStart, cancellationToken);

        var requestsThisMonth = await _context.UsageLogs
            .CountAsync(u => u.CreatedAt >= monthStart, cancellationToken);

        var totalCostToday = await _context.UsageLogs
            .Where(u => u.CreatedAt >= todayStart)
            .SumAsync(u => u.CostUsd, cancellationToken);

        var totalCostThisMonth = await _context.UsageLogs
            .Where(u => u.CreatedAt >= monthStart)
            .SumAsync(u => u.CostUsd, cancellationToken);

        return Result<GlobalSummaryDto>.Success(new GlobalSummaryDto
        {
            TotalProjects = totalProjects,
            ActiveProjects = activeProjects,
            RequestsToday = requestsToday,
            RequestsThisMonth = requestsThisMonth,
            TotalCostToday = totalCostToday,
            TotalCostThisMonth = totalCostThisMonth
        });
    }
}

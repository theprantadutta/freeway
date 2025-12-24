using Freeway.Application.Common;
using Freeway.Application.DTOs;
using Freeway.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Freeway.Application.Features.Analytics.Queries;

public class GetUsageLogsQueryHandler : IRequestHandler<GetUsageLogsQuery, Result<UsageLogsResponseDto>>
{
    private readonly IAppDbContext _context;

    public GetUsageLogsQueryHandler(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<Result<UsageLogsResponseDto>> Handle(GetUsageLogsQuery request, CancellationToken cancellationToken)
    {
        var project = await _context.Projects
            .AnyAsync(p => p.Id == request.ProjectId, cancellationToken);

        if (!project)
        {
            return Result<UsageLogsResponseDto>.NotFound("Project not found");
        }

        var query = _context.UsageLogs.Where(u => u.ProjectId == request.ProjectId);

        if (request.StartDate.HasValue)
            query = query.Where(u => u.CreatedAt >= request.StartDate.Value);

        if (request.EndDate.HasValue)
            query = query.Where(u => u.CreatedAt <= request.EndDate.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var logs = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip(request.Offset)
            .Take(Math.Min(request.Limit, 1000))
            .Select(u => new UsageLogDto
            {
                Id = u.Id,
                ProjectId = u.ProjectId,
                ModelId = u.ModelId,
                ModelType = u.ModelType,
                InputTokens = u.InputTokens,
                OutputTokens = u.OutputTokens,
                ResponseTimeMs = u.ResponseTimeMs,
                CostUsd = u.CostUsd,
                Success = u.Success,
                ErrorMessage = u.ErrorMessage,
                RequestId = u.RequestId,
                CreatedAt = u.CreatedAt,
                Provider = u.Provider,
                RequestMessages = u.RequestMessages != null
                    ? u.RequestMessages.Select(m => new ChatMessageDto { Role = m.Role, Content = m.Content }).ToList()
                    : null,
                ResponseContent = u.ResponseContent,
                FinishReason = u.FinishReason
            })
            .ToListAsync(cancellationToken);

        return Result<UsageLogsResponseDto>.Success(new UsageLogsResponseDto
        {
            Logs = logs,
            TotalCount = totalCount,
            Limit = request.Limit,
            Offset = request.Offset
        });
    }
}

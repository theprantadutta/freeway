using Freeway.Application.Common;
using Freeway.Application.DTOs;
using MediatR;

namespace Freeway.Application.Features.Analytics.Queries;

public record GetUsageLogsQuery(
    Guid ProjectId,
    int Limit = 100,
    int Offset = 0,
    DateTime? StartDate = null,
    DateTime? EndDate = null
) : IRequest<Result<UsageLogsResponseDto>>;

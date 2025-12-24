using Freeway.Application.Common;
using Freeway.Application.DTOs;
using MediatR;

namespace Freeway.Application.Features.Analytics.Queries;

public record GetProjectUsageQuery(
    Guid ProjectId,
    DateTime? StartDate = null,
    DateTime? EndDate = null
) : IRequest<Result<ProjectUsageDto>>;

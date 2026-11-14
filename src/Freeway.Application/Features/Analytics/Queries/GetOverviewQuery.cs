using Freeway.Application.Common;
using Freeway.Application.DTOs;
using MediatR;

namespace Freeway.Application.Features.Analytics.Queries;

/// <summary>
/// Assembles the whole dashboard in one query. <paramref name="Days"/> is the span of
/// the daily series, clamped to a sane range by the handler. <paramref name="ProjectId"/>
/// limits every figure to one project; null covers the whole gateway.
/// </summary>
public record GetOverviewQuery(int Days = 30, Guid? ProjectId = null) : IRequest<Result<OverviewDto>>;

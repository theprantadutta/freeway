using Freeway.Application.Common;
using Freeway.Application.DTOs;
using MediatR;

namespace Freeway.Application.Features.Analytics.Queries;

/// <summary>
/// Assembles the whole dashboard in one query. <paramref name="Days"/> is the span of
/// the daily series, clamped to a sane range by the handler.
/// </summary>
public record GetOverviewQuery(int Days = 30) : IRequest<Result<OverviewDto>>;

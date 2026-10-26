using Freeway.Application.Common;
using Freeway.Application.DTOs;
using MediatR;

namespace Freeway.Application.Features.Credits.Queries;

/// <summary>The Claude API credit: where the current cycle stands and what spent it.</summary>
public record GetCreditOverviewQuery : IRequest<Result<CreditOverviewDto>>;

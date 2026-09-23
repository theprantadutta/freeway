using Freeway.Application.Common;
using Freeway.Application.DTOs;
using Freeway.Domain.Common;
using MediatR;

namespace Freeway.Application.Features.Models.Queries;

/// <summary>
/// Lists paid models. A null <paramref name="Tier"/> returns every paid model cheapest-first,
/// which is what /models/paid has always served. A tier returns that tier's preference chain.
/// </summary>
public record GetPaidModelsQuery(PaidTier? Tier = null) : IRequest<Result<ModelsListDto>>;

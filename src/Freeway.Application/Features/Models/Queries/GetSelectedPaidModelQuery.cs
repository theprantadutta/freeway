using Freeway.Application.Common;
using Freeway.Application.DTOs;
using Freeway.Domain.Common;
using MediatR;

namespace Freeway.Application.Features.Models.Queries;

/// <summary>
/// Returns the selected model for a paid tier. A null <paramref name="Tier"/> means the
/// default tier (Low), which is what /model/paid has always returned.
/// </summary>
public record GetSelectedPaidModelQuery(PaidTier? Tier = null) : IRequest<Result<SelectedModelDto>>;

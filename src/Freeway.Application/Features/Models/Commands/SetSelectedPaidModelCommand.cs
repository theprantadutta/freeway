using Freeway.Application.Common;
using Freeway.Application.DTOs;
using Freeway.Domain.Common;
using MediatR;

namespace Freeway.Application.Features.Models.Commands;

/// <summary>
/// Sets the selected model for a paid tier. A null <paramref name="Tier"/> means the default
/// tier (Low), which is what PUT /admin/model/paid has always set.
/// </summary>
public record SetSelectedPaidModelCommand(string ModelId, PaidTier? Tier = null)
    : IRequest<Result<SetModelResponseDto>>;

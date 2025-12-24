using Freeway.Application.Common;
using Freeway.Application.DTOs;
using MediatR;

namespace Freeway.Application.Features.Models.Commands;

public record SetSelectedPaidModelCommand(string ModelId) : IRequest<Result<SetModelResponseDto>>;

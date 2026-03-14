using Freeway.Application.Common;
using Freeway.Application.DTOs;
using MediatR;

namespace Freeway.Application.Features.Models.Commands;

public record SetSelectedImageModelCommand(string ModelId) : IRequest<Result<SetModelResponseDto>>;

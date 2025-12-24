using Freeway.Application.Common;
using Freeway.Application.DTOs;
using MediatR;

namespace Freeway.Application.Features.Projects.Commands;

public record RotateProjectKeyCommand(Guid Id) : IRequest<Result<RotateKeyResultDto>>;

using Freeway.Application.Common;
using MediatR;

namespace Freeway.Application.Features.Projects.Commands;

public record DeleteProjectCommand(Guid Id) : IRequest<Result<bool>>;

using Freeway.Application.Common;
using Freeway.Application.DTOs;
using MediatR;

namespace Freeway.Application.Features.Projects.Queries;

public record GetProjectByIdQuery(Guid Id) : IRequest<Result<ProjectDto>>;

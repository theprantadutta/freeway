using Freeway.Application.Common;
using Freeway.Application.DTOs;
using MediatR;

namespace Freeway.Application.Features.Projects.Queries;

public record GetProjectsQuery : IRequest<Result<ProjectsListDto>>;

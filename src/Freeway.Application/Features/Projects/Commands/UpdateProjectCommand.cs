using Freeway.Application.Common;
using Freeway.Application.DTOs;
using MediatR;

namespace Freeway.Application.Features.Projects.Commands;

public record UpdateProjectCommand(
    Guid Id,
    string? Name = null,
    bool? IsActive = null,
    int? RateLimitPerMinute = null,
    Dictionary<string, object>? Metadata = null
) : IRequest<Result<ProjectDto>>;

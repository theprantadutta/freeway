using Freeway.Application.Common;
using Freeway.Application.DTOs;
using MediatR;

namespace Freeway.Application.Features.Projects.Commands;

public record CreateProjectCommand(
    string Name,
    int RateLimitPerMinute = 60,
    Dictionary<string, object>? Metadata = null
) : IRequest<Result<ProjectWithKeyDto>>;

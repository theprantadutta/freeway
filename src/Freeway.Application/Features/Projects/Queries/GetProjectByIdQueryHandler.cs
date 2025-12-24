using Freeway.Application.Common;
using Freeway.Application.DTOs;
using Freeway.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Freeway.Application.Features.Projects.Queries;

public class GetProjectByIdQueryHandler : IRequestHandler<GetProjectByIdQuery, Result<ProjectDto>>
{
    private readonly IAppDbContext _context;

    public GetProjectByIdQueryHandler(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<Result<ProjectDto>> Handle(GetProjectByIdQuery request, CancellationToken cancellationToken)
    {
        var project = await _context.Projects
            .Where(p => p.Id == request.Id)
            .Select(p => new ProjectDto
            {
                Id = p.Id,
                Name = p.Name,
                ApiKeyPrefix = p.ApiKeyPrefix,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt,
                IsActive = p.IsActive,
                RateLimitPerMinute = p.RateLimitPerMinute,
                Metadata = p.Metadata
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (project == null)
        {
            return Result<ProjectDto>.NotFound("Project not found");
        }

        return Result<ProjectDto>.Success(project);
    }
}

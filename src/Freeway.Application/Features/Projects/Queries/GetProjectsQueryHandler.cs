using Freeway.Application.Common;
using Freeway.Application.DTOs;
using Freeway.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Freeway.Application.Features.Projects.Queries;

public class GetProjectsQueryHandler : IRequestHandler<GetProjectsQuery, Result<ProjectsListDto>>
{
    private readonly IAppDbContext _context;

    public GetProjectsQueryHandler(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<Result<ProjectsListDto>> Handle(GetProjectsQuery request, CancellationToken cancellationToken)
    {
        var projects = await _context.Projects
            .OrderByDescending(p => p.CreatedAt)
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
            .ToListAsync(cancellationToken);

        return Result<ProjectsListDto>.Success(new ProjectsListDto
        {
            Projects = projects,
            TotalCount = projects.Count
        });
    }
}

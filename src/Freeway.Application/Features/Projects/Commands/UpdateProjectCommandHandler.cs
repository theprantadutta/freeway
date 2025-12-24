using Freeway.Application.Common;
using Freeway.Application.DTOs;
using Freeway.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Freeway.Application.Features.Projects.Commands;

public class UpdateProjectCommandHandler : IRequestHandler<UpdateProjectCommand, Result<ProjectDto>>
{
    private readonly IAppDbContext _context;
    private readonly IProjectCacheService _projectCacheService;
    private readonly IDateTimeService _dateTimeService;

    public UpdateProjectCommandHandler(
        IAppDbContext context,
        IProjectCacheService projectCacheService,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _projectCacheService = projectCacheService;
        _dateTimeService = dateTimeService;
    }

    public async Task<Result<ProjectDto>> Handle(UpdateProjectCommand request, CancellationToken cancellationToken)
    {
        var project = await _context.Projects
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        if (project == null)
        {
            return Result<ProjectDto>.NotFound("Project not found");
        }

        if (request.Name != null)
            project.Name = request.Name;

        if (request.IsActive.HasValue)
            project.IsActive = request.IsActive.Value;

        if (request.RateLimitPerMinute.HasValue)
            project.RateLimitPerMinute = request.RateLimitPerMinute.Value;

        if (request.Metadata != null)
            project.Metadata = request.Metadata;

        project.UpdatedAt = _dateTimeService.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        // Invalidate cache if active status changed
        _projectCacheService.InvalidateCache();

        return Result<ProjectDto>.Success(new ProjectDto
        {
            Id = project.Id,
            Name = project.Name,
            ApiKeyPrefix = project.ApiKeyPrefix,
            CreatedAt = project.CreatedAt,
            UpdatedAt = project.UpdatedAt,
            IsActive = project.IsActive,
            RateLimitPerMinute = project.RateLimitPerMinute,
            Metadata = project.Metadata
        });
    }
}

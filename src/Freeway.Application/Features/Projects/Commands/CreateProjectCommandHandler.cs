using Freeway.Application.Common;
using Freeway.Application.DTOs;
using Freeway.Domain.Entities;
using Freeway.Domain.Interfaces;
using MediatR;

namespace Freeway.Application.Features.Projects.Commands;

public class CreateProjectCommandHandler : IRequestHandler<CreateProjectCommand, Result<ProjectWithKeyDto>>
{
    private readonly IAppDbContext _context;
    private readonly IApiKeyService _apiKeyService;
    private readonly IProjectCacheService _projectCacheService;
    private readonly IDateTimeService _dateTimeService;

    public CreateProjectCommandHandler(
        IAppDbContext context,
        IApiKeyService apiKeyService,
        IProjectCacheService projectCacheService,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _apiKeyService = apiKeyService;
        _projectCacheService = projectCacheService;
        _dateTimeService = dateTimeService;
    }

    public async Task<Result<ProjectWithKeyDto>> Handle(CreateProjectCommand request, CancellationToken cancellationToken)
    {
        var (rawKey, hash, prefix) = _apiKeyService.GenerateApiKey();

        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            ApiKeyHash = hash,
            ApiKeyPrefix = prefix,
            RateLimitPerMinute = request.RateLimitPerMinute,
            Metadata = request.Metadata,
            IsActive = true,
            CreatedAt = _dateTimeService.UtcNow,
            UpdatedAt = _dateTimeService.UtcNow
        };

        _context.Projects.Add(project);
        await _context.SaveChangesAsync(cancellationToken);

        // Invalidate cache to include new project
        _projectCacheService.InvalidateCache();

        return Result<ProjectWithKeyDto>.Created(new ProjectWithKeyDto
        {
            Id = project.Id,
            Name = project.Name,
            ApiKeyPrefix = project.ApiKeyPrefix,
            ApiKey = rawKey,
            CreatedAt = project.CreatedAt,
            UpdatedAt = project.UpdatedAt,
            IsActive = project.IsActive,
            RateLimitPerMinute = project.RateLimitPerMinute,
            Metadata = project.Metadata
        });
    }
}

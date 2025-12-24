using Freeway.Application.Common;
using Freeway.Application.DTOs;
using Freeway.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Freeway.Application.Features.Projects.Commands;

public class RotateProjectKeyCommandHandler : IRequestHandler<RotateProjectKeyCommand, Result<RotateKeyResultDto>>
{
    private readonly IAppDbContext _context;
    private readonly IApiKeyService _apiKeyService;
    private readonly IProjectCacheService _projectCacheService;
    private readonly IDateTimeService _dateTimeService;

    public RotateProjectKeyCommandHandler(
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

    public async Task<Result<RotateKeyResultDto>> Handle(RotateProjectKeyCommand request, CancellationToken cancellationToken)
    {
        var project = await _context.Projects
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        if (project == null)
        {
            return Result<RotateKeyResultDto>.NotFound("Project not found");
        }

        var (rawKey, hash, prefix) = _apiKeyService.GenerateApiKey();

        project.ApiKeyHash = hash;
        project.ApiKeyPrefix = prefix;
        project.UpdatedAt = _dateTimeService.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        // Invalidate cache immediately (old key is now invalid)
        _projectCacheService.InvalidateCache();

        return Result<RotateKeyResultDto>.Success(new RotateKeyResultDto
        {
            Id = project.Id,
            ApiKey = rawKey,
            ApiKeyPrefix = prefix
        });
    }
}

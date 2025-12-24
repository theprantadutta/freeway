using Freeway.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Freeway.Infrastructure.Jobs;

public class BackgroundJobService : IBackgroundJobService
{
    private readonly IModelCacheService _modelCacheService;
    private readonly IProjectCacheService _projectCacheService;
    private readonly ILogger<BackgroundJobService> _logger;

    public BackgroundJobService(
        IModelCacheService modelCacheService,
        IProjectCacheService projectCacheService,
        ILogger<BackgroundJobService> logger)
    {
        _modelCacheService = modelCacheService;
        _projectCacheService = projectCacheService;
        _logger = logger;
    }

    public async Task RefreshModelsAsync()
    {
        _logger.LogInformation("Running scheduled model refresh job...");
        await _modelCacheService.RefreshModelsAsync();
        _logger.LogInformation("Model refresh job completed");
    }

    public async Task RefreshProjectCacheAsync()
    {
        _logger.LogInformation("Running scheduled project cache refresh job...");
        await _projectCacheService.LoadCacheAsync();
        _logger.LogInformation("Project cache refresh job completed");
    }
}

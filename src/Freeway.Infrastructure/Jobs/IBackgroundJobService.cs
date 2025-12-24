namespace Freeway.Infrastructure.Jobs;

public interface IBackgroundJobService
{
    Task RefreshModelsAsync();
    Task RefreshProjectCacheAsync();
}

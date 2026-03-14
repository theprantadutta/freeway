using Freeway.Application.Common;
using Freeway.Application.DTOs;
using Freeway.Domain.Interfaces;
using MediatR;

namespace Freeway.Application.Features.Health.Queries;

public class GetHealthQueryHandler : IRequestHandler<GetHealthQuery, Result<HealthResponseDto>>
{
    private readonly IModelCacheService _modelCacheService;

    public GetHealthQueryHandler(IModelCacheService modelCacheService)
    {
        _modelCacheService = modelCacheService;
    }

    public Task<Result<HealthResponseDto>> Handle(GetHealthQuery request, CancellationToken cancellationToken)
    {
        var freeModels = _modelCacheService.GetFreeModels();
        var paidModels = _modelCacheService.GetPaidModels();
        var imageModels = _modelCacheService.GetImageModels();
        var selectedFree = _modelCacheService.GetSelectedFreeModel();
        var selectedPaid = _modelCacheService.GetSelectedPaidModel();
        var selectedImage = _modelCacheService.GetSelectedImageModel();

        return Task.FromResult(Result<HealthResponseDto>.Success(new HealthResponseDto
        {
            Status = "healthy",
            Service = "freeway",
            Version = "1.0.0",
            FreeModelsCount = freeModels.Count,
            PaidModelsCount = paidModels.Count,
            ImageModelsCount = imageModels.Count,
            SelectedFreeModel = selectedFree?.Id,
            SelectedPaidModel = selectedPaid?.Id,
            SelectedImageModel = selectedImage?.Id,
            LastRefresh = _modelCacheService.GetLastUpdated()
        }));
    }
}

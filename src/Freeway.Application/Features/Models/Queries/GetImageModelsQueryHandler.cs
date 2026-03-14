using Freeway.Application.Common;
using Freeway.Application.DTOs;
using Freeway.Domain.Interfaces;
using MediatR;

namespace Freeway.Application.Features.Models.Queries;

public class GetImageModelsQueryHandler : IRequestHandler<GetImageModelsQuery, Result<ModelsListDto>>
{
    private readonly IModelCacheService _modelCacheService;

    public GetImageModelsQueryHandler(IModelCacheService modelCacheService)
    {
        _modelCacheService = modelCacheService;
    }

    public Task<Result<ModelsListDto>> Handle(GetImageModelsQuery request, CancellationToken cancellationToken)
    {
        var models = _modelCacheService.GetImageModels();

        var result = new ModelsListDto
        {
            Models = models.Select(m => new ModelInfoDto
            {
                ModelId = m.Id,
                ModelName = m.Name,
                Description = m.Description,
                ContextLength = m.ContextLength,
                Pricing = new PricingInfoDto
                {
                    Prompt = m.PromptPrice,
                    Completion = m.CompletionPrice
                },
                Rank = m.Rank
            }).ToList(),
            TotalCount = models.Count,
            LastUpdated = _modelCacheService.GetLastUpdated()
        };

        return Task.FromResult(Result<ModelsListDto>.Success(result));
    }
}

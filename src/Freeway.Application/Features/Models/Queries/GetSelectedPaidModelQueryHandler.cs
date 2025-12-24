using Freeway.Application.Common;
using Freeway.Application.DTOs;
using Freeway.Domain.Interfaces;
using MediatR;

namespace Freeway.Application.Features.Models.Queries;

public class GetSelectedPaidModelQueryHandler : IRequestHandler<GetSelectedPaidModelQuery, Result<SelectedModelDto>>
{
    private readonly IModelCacheService _modelCacheService;

    public GetSelectedPaidModelQueryHandler(IModelCacheService modelCacheService)
    {
        _modelCacheService = modelCacheService;
    }

    public Task<Result<SelectedModelDto>> Handle(GetSelectedPaidModelQuery request, CancellationToken cancellationToken)
    {
        var model = _modelCacheService.GetSelectedPaidModel();

        if (model == null)
        {
            return Task.FromResult(Result<SelectedModelDto>.ServiceUnavailable("No paid models available"));
        }

        return Task.FromResult(Result<SelectedModelDto>.Success(new SelectedModelDto
        {
            ModelId = model.Id,
            ModelName = model.Name,
            Description = model.Description,
            ContextLength = model.ContextLength,
            Pricing = new PricingInfoDto
            {
                Prompt = model.PromptPrice,
                Completion = model.CompletionPrice
            }
        }));
    }
}

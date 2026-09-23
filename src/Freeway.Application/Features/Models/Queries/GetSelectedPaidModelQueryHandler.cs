using Freeway.Application.Common;
using Freeway.Application.DTOs;
using Freeway.Domain.Common;
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
        var tier = request.Tier ?? PaidTier.Low;
        var model = _modelCacheService.GetSelectedPaidModel(tier);

        if (model == null)
        {
            return Task.FromResult(Result<SelectedModelDto>.ServiceUnavailable(
                request.Tier is null
                    ? "No paid models available"
                    : $"No models available for the '{tier.ToSlug()}' paid tier"));
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
            },
            Tier = model.Tier?.ToSlug(),
            IsCurated = model.IsCurated
        }));
    }
}

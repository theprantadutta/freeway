using Freeway.Application.Common;
using Freeway.Application.DTOs;
using Freeway.Domain.Common;
using Freeway.Domain.Interfaces;
using MediatR;

namespace Freeway.Application.Features.Models.Commands;

public class SetSelectedPaidModelCommandHandler : IRequestHandler<SetSelectedPaidModelCommand, Result<SetModelResponseDto>>
{
    private readonly IModelCacheService _modelCacheService;

    public SetSelectedPaidModelCommandHandler(IModelCacheService modelCacheService)
    {
        _modelCacheService = modelCacheService;
    }

    public Task<Result<SetModelResponseDto>> Handle(SetSelectedPaidModelCommand request, CancellationToken cancellationToken)
    {
        var model = _modelCacheService.GetModelById(request.ModelId);

        if (model == null || model.IsFree)
        {
            return Task.FromResult(Result<SetModelResponseDto>.NotFound($"Paid model '{request.ModelId}' not found"));
        }

        // An explicit tier must match the model. The untiered legacy call (PUT /admin/model/paid)
        // targets whichever tier the model actually belongs to, so selecting any paid model keeps
        // succeeding exactly as it did before tiers existed.
        if (request.Tier is { } requestedTier && model.Tier is { } actualTier && requestedTier != actualTier)
        {
            return Task.FromResult(Result<SetModelResponseDto>.Failure(
                $"Model '{model.Id}' belongs to the '{actualTier.ToSlug()}' tier, not '{requestedTier.ToSlug()}'.",
                400));
        }

        var targetTier = request.Tier ?? model.Tier ?? PaidTier.Low;

        if (!_modelCacheService.SetSelectedPaidModel(request.ModelId, targetTier))
        {
            return Task.FromResult(Result<SetModelResponseDto>.NotFound(
                $"Paid model '{request.ModelId}' is not available in the '{targetTier.ToSlug()}' tier"));
        }

        return Task.FromResult(Result<SetModelResponseDto>.Success(new SetModelResponseDto
        {
            Success = true,
            ModelId = model.Id,
            ModelName = model.Name,
            Tier = targetTier.ToSlug(),
            Message = $"Selected {targetTier.ToSlug()} paid model set to '{model.Name}'"
        }));
    }
}

using Freeway.Application.Common;
using Freeway.Application.DTOs;
using Freeway.Domain.Interfaces;
using MediatR;

namespace Freeway.Application.Features.Models.Commands;

public class SetSelectedFreeModelCommandHandler : IRequestHandler<SetSelectedFreeModelCommand, Result<SetModelResponseDto>>
{
    private readonly IModelCacheService _modelCacheService;

    public SetSelectedFreeModelCommandHandler(IModelCacheService modelCacheService)
    {
        _modelCacheService = modelCacheService;
    }

    public Task<Result<SetModelResponseDto>> Handle(SetSelectedFreeModelCommand request, CancellationToken cancellationToken)
    {
        var model = _modelCacheService.GetModelById(request.ModelId);

        if (model == null || !model.IsFree)
        {
            return Task.FromResult(Result<SetModelResponseDto>.NotFound($"Free model '{request.ModelId}' not found"));
        }

        _modelCacheService.SetSelectedFreeModel(request.ModelId);

        return Task.FromResult(Result<SetModelResponseDto>.Success(new SetModelResponseDto
        {
            Success = true,
            ModelId = model.Id,
            ModelName = model.Name,
            Message = $"Selected free model set to '{model.Name}'"
        }));
    }
}

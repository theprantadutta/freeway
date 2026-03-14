using Freeway.Application.Common;
using Freeway.Application.DTOs;
using Freeway.Domain.Interfaces;
using MediatR;

namespace Freeway.Application.Features.Models.Commands;

public class SetSelectedImageModelCommandHandler : IRequestHandler<SetSelectedImageModelCommand, Result<SetModelResponseDto>>
{
    private readonly IModelCacheService _modelCacheService;

    public SetSelectedImageModelCommandHandler(IModelCacheService modelCacheService)
    {
        _modelCacheService = modelCacheService;
    }

    public Task<Result<SetModelResponseDto>> Handle(SetSelectedImageModelCommand request, CancellationToken cancellationToken)
    {
        var model = _modelCacheService.GetModelById(request.ModelId);

        if (model == null || !model.IsImageModel)
        {
            return Task.FromResult(Result<SetModelResponseDto>.NotFound($"Image model '{request.ModelId}' not found"));
        }

        _modelCacheService.SetSelectedImageModel(request.ModelId);

        return Task.FromResult(Result<SetModelResponseDto>.Success(new SetModelResponseDto
        {
            Success = true,
            ModelId = model.Id,
            ModelName = model.Name,
            Message = $"Selected image model set to '{model.Name}'"
        }));
    }
}

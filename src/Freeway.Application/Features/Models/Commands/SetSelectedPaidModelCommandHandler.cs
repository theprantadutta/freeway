using Freeway.Application.Common;
using Freeway.Application.DTOs;
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

        _modelCacheService.SetSelectedPaidModel(request.ModelId);

        return Task.FromResult(Result<SetModelResponseDto>.Success(new SetModelResponseDto
        {
            Success = true,
            ModelId = model.Id,
            ModelName = model.Name,
            Message = $"Selected paid model set to '{model.Name}'"
        }));
    }
}

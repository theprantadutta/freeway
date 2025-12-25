using Freeway.Application.Common;
using Freeway.Application.DTOs;
using Freeway.Domain.Interfaces;
using MediatR;

namespace Freeway.Application.Features.Models.Queries;

public class GetProviderModelsQueryHandler : IRequestHandler<GetProviderModelsQuery, Result<ProviderModelsListDto>>
{
    private readonly IProviderModelCache _providerModelCache;

    public GetProviderModelsQueryHandler(IProviderModelCache providerModelCache)
    {
        _providerModelCache = providerModelCache;
    }

    public Task<Result<ProviderModelsListDto>> Handle(GetProviderModelsQuery request, CancellationToken cancellationToken)
    {
        var allModels = _providerModelCache.GetAllProviderModels();
        var summary = _providerModelCache.GetCacheSummary();

        var data = new List<ProviderModelDto>();

        foreach (var (providerName, models) in allModels)
        {
            // Filter by provider if specified
            if (!string.IsNullOrEmpty(request.ProviderFilter) &&
                !providerName.Equals(request.ProviderFilter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var model in models)
            {
                data.Add(new ProviderModelDto
                {
                    Id = model.Id,
                    Name = model.Name,
                    Provider = model.ProviderName,
                    Description = model.Description,
                    ContextLength = model.ContextLength,
                    OwnedBy = model.OwnedBy,
                    CreatedAt = model.CreatedAt
                });
            }
        }

        var result = new ProviderModelsListDto
        {
            Data = data.OrderBy(m => m.Provider).ThenBy(m => m.Id).ToList(),
            TotalCount = data.Count,
            ProviderCount = summary.ProviderCount,
            LastUpdatedByProvider = summary.LastValidatedByProvider
        };

        return Task.FromResult(Result<ProviderModelsListDto>.Success(result));
    }
}

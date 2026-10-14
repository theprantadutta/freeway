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

        // The gateway's own model names come first: they are what most callers should
        // send, and a client listing models would otherwise never learn they exist.
        var virtualModels = string.IsNullOrEmpty(request.ProviderFilter) ||
                            request.ProviderFilter.Equals(GatewayOwner, StringComparison.OrdinalIgnoreCase)
            ? VirtualModels
            : [];

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
                    OwnedBy = model.OwnedBy ?? model.ProviderName,
                    Created = model.CreatedAt is { } created
                        ? new DateTimeOffset(DateTime.SpecifyKind(created, DateTimeKind.Utc)).ToUnixTimeSeconds()
                        : 0,
                    CreatedAt = model.CreatedAt
                });
            }
        }

        var result = new ProviderModelsListDto
        {
            Data = virtualModels.Concat(data.OrderBy(m => m.Provider).ThenBy(m => m.Id)).ToList(),
            TotalCount = virtualModels.Count + data.Count,
            ProviderCount = summary.ProviderCount,
            LastUpdatedByProvider = summary.LastValidatedByProvider
        };

        return Task.FromResult(Result<ProviderModelsListDto>.Success(result));
    }

    private const string GatewayOwner = "freeway";

    /// <summary>The routing names accepted as "model" on /chat/completions.</summary>
    private static readonly List<ProviderModelDto> VirtualModels =
    [
        Virtual("free", "Free lane", "Free providers only. Never incurs a charge; 503 rather than a paid fallback."),
        Virtual("paid", "Paid (low)", "Alias for paid:low."),
        Virtual("paid:low", "Paid: low", "Cheapest paid models, with fallback."),
        Virtual("paid:moderate", "Paid: moderate", "Balanced cost and capability, with fallback."),
        Virtual("paid:premium", "Paid: premium", "Highest-capability models, with fallback."),
        Virtual("image", "Image", "Cheapest image generation model.")
    ];

    private static ProviderModelDto Virtual(string id, string name, string description) => new()
    {
        Id = id,
        Name = name,
        Provider = GatewayOwner,
        OwnedBy = GatewayOwner,
        Description = description
    };
}

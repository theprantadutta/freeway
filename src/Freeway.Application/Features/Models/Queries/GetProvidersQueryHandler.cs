using Freeway.Application.Common;
using Freeway.Application.DTOs;
using Freeway.Domain.Interfaces;
using MediatR;

namespace Freeway.Application.Features.Models.Queries;

public class GetProvidersQueryHandler : IRequestHandler<GetProvidersQuery, Result<ProvidersListDto>>
{
    private readonly IEnumerable<IAiProvider> _providers;
    private readonly IProviderModelCache _providerModelCache;
    private readonly IProviderBenchmarkCache _benchmarkCache;

    public GetProvidersQueryHandler(
        IEnumerable<IAiProvider> providers,
        IProviderModelCache providerModelCache,
        IProviderBenchmarkCache benchmarkCache)
    {
        _providers = providers;
        _providerModelCache = providerModelCache;
        _benchmarkCache = benchmarkCache;
    }

    public Task<Result<ProvidersListDto>> Handle(GetProvidersQuery request, CancellationToken cancellationToken)
    {
        var summary = _providerModelCache.GetCacheSummary();
        var rankings = _benchmarkCache.GetRankedProviders();

        var providerDtos = _providers.Select(p =>
        {
            var modelCount = summary.ModelCountByProvider.TryGetValue(p.Name, out var count) ? count : 0;
            var lastValidated = summary.LastValidatedByProvider.TryGetValue(p.Name, out var dt) ? dt : null;
            var benchmarkScore = _benchmarkCache.GetProviderScore(p.Name);
            var rank = rankings.IndexOf(p.Name) + 1;

            return new ProviderDto
            {
                Name = p.Name,
                DisplayName = p.DisplayName,
                IsEnabled = p.IsEnabled,
                IsFreeProvider = p.IsFreeProvider,
                DefaultModelId = p.DefaultModelId,
                ModelCount = modelCount,
                LastValidated = lastValidated,
                BenchmarkRank = rank > 0 ? rank : null,
                SuccessRate = benchmarkScore?.SuccessRate,
                AvgResponseTimeMs = benchmarkScore?.AvgResponseTimeMs
            };
        }).OrderBy(p => rankings.IndexOf(p.Name) >= 0 ? rankings.IndexOf(p.Name) : int.MaxValue)
          .ToList();

        var result = new ProvidersListDto
        {
            Providers = providerDtos,
            TotalCount = providerDtos.Count,
            EnabledCount = providerDtos.Count(p => p.IsEnabled),
            FreeProviderCount = providerDtos.Count(p => p.IsFreeProvider)
        };

        return Task.FromResult(Result<ProvidersListDto>.Success(result));
    }
}

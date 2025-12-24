using Freeway.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Freeway.Infrastructure.Services;

public class ModelCacheService : IModelCacheService
{
    private readonly IOpenRouterService _openRouterService;
    private readonly ILogger<ModelCacheService> _logger;
    private readonly object _lock = new();

    private List<CachedModel> _freeModels = new();
    private List<CachedModel> _paidModels = new();
    private CachedModel? _selectedFreeModel;
    private CachedModel? _selectedPaidModel;
    private DateTime? _lastUpdated;

    public ModelCacheService(IOpenRouterService openRouterService, ILogger<ModelCacheService> logger)
    {
        _openRouterService = openRouterService;
        _logger = logger;
    }

    public List<CachedModel> GetFreeModels()
    {
        lock (_lock)
        {
            return _freeModels.ToList();
        }
    }

    public List<CachedModel> GetPaidModels()
    {
        lock (_lock)
        {
            return _paidModels.ToList();
        }
    }

    public CachedModel? GetSelectedFreeModel()
    {
        lock (_lock)
        {
            return _selectedFreeModel;
        }
    }

    public CachedModel? GetSelectedPaidModel()
    {
        lock (_lock)
        {
            return _selectedPaidModel;
        }
    }

    public CachedModel? GetModelById(string modelId)
    {
        lock (_lock)
        {
            return _freeModels.FirstOrDefault(m => m.Id == modelId)
                   ?? _paidModels.FirstOrDefault(m => m.Id == modelId);
        }
    }

    public void SetSelectedFreeModel(string modelId)
    {
        lock (_lock)
        {
            var model = _freeModels.FirstOrDefault(m => m.Id == modelId);
            if (model != null)
            {
                _selectedFreeModel = model;
                _logger.LogInformation("Selected free model set to: {ModelId}", modelId);
            }
        }
    }

    public void SetSelectedPaidModel(string modelId)
    {
        lock (_lock)
        {
            var model = _paidModels.FirstOrDefault(m => m.Id == modelId);
            if (model != null)
            {
                _selectedPaidModel = model;
                _logger.LogInformation("Selected paid model set to: {ModelId}", modelId);
            }
        }
    }

    public DateTime? GetLastUpdated()
    {
        lock (_lock)
        {
            return _lastUpdated;
        }
    }

    public async Task RefreshModelsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Refreshing models from OpenRouter...");

        try
        {
            var models = await _openRouterService.GetModelsAsync(cancellationToken);

            if (models.Count == 0)
            {
                _logger.LogWarning("No models returned from OpenRouter");
                return;
            }

            var freeModels = new List<CachedModel>();
            var paidModels = new List<CachedModel>();

            foreach (var model in models)
            {
                var isFree = IsFreeModel(model);
                var cachedModel = new CachedModel
                {
                    Id = model.Id,
                    Name = model.Name,
                    Description = model.Description,
                    ContextLength = model.ContextLength,
                    PromptPrice = model.Pricing.Prompt,
                    CompletionPrice = model.Pricing.Completion,
                    IsFree = isFree
                };

                if (isFree)
                {
                    freeModels.Add(cachedModel);
                }
                else if (IsValidPaidModel(model))
                {
                    paidModels.Add(cachedModel);
                }
            }

            // Rank free models by context length (descending)
            freeModels = freeModels
                .OrderByDescending(m => m.ContextLength ?? 0)
                .Select((m, i) => { m.Rank = i + 1; return m; })
                .ToList();

            // Rank paid models by price (ascending)
            paidModels = paidModels
                .OrderBy(m => GetTotalPrice(m))
                .Select((m, i) => { m.Rank = i + 1; return m; })
                .ToList();

            lock (_lock)
            {
                _freeModels = freeModels;
                _paidModels = paidModels;
                _lastUpdated = DateTime.UtcNow;

                // Auto-select best free model (largest context)
                if (_selectedFreeModel == null || !freeModels.Any(m => m.Id == _selectedFreeModel.Id))
                {
                    _selectedFreeModel = freeModels.FirstOrDefault();
                    if (_selectedFreeModel != null)
                    {
                        _logger.LogInformation("Auto-selected best free model: {ModelId} (context: {Context})",
                            _selectedFreeModel.Id, _selectedFreeModel.ContextLength);
                    }
                }

                // Auto-select cheapest paid model (only on first load)
                if (_selectedPaidModel == null)
                {
                    _selectedPaidModel = paidModels.FirstOrDefault();
                    if (_selectedPaidModel != null)
                    {
                        _logger.LogInformation("Auto-selected cheapest paid model: {ModelId} (price: {Price})",
                            _selectedPaidModel.Id, GetTotalPrice(_selectedPaidModel));
                    }
                }
            }

            _logger.LogInformation("Model cache refreshed: {FreeCount} free, {PaidCount} paid models",
                freeModels.Count, paidModels.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh models");
        }
    }

    private static bool IsFreeModel(OpenRouterModel model)
    {
        // Model is free if ID ends with ":free" or both prices are "0"
        if (model.Id.EndsWith(":free", StringComparison.OrdinalIgnoreCase))
            return true;

        return model.Pricing.Prompt == "0" && model.Pricing.Completion == "0";
    }

    private static bool IsValidPaidModel(OpenRouterModel model)
    {
        // Exclude variable pricing models
        if (model.Id.Contains("/auto", StringComparison.OrdinalIgnoreCase) ||
            model.Id.Contains("router", StringComparison.OrdinalIgnoreCase))
            return false;

        // Require minimum context length
        if (model.ContextLength < 8000)
            return false;

        // Must have parseable numeric prices
        if (!decimal.TryParse(model.Pricing.Prompt, out _) ||
            !decimal.TryParse(model.Pricing.Completion, out _))
            return false;

        return true;
    }

    private static decimal GetTotalPrice(CachedModel model)
    {
        decimal.TryParse(model.PromptPrice, out var prompt);
        decimal.TryParse(model.CompletionPrice, out var completion);
        return prompt + completion;
    }
}

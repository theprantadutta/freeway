using System.Globalization;
using Freeway.Domain.Common;
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
    private List<CachedModel> _imageModels = new();
    private Dictionary<PaidTier, List<CachedModel>> _paidModelsByTier = EmptyTierMap();
    private CachedModel? _selectedFreeModel;
    private Dictionary<PaidTier, CachedModel?> _selectedPaidByTier = new();
    private CachedModel? _selectedImageModel;
    private DateTime? _lastUpdated;

    // Model id -> tier, for models explicitly named in a tier's curated list. Curation wins
    // over the price band so a curated model never drifts between tiers on a price change.
    private static readonly Dictionary<string, PaidTier> CuratedTiers = BuildCuratedTiers();

    public ModelCacheService(IOpenRouterService openRouterService, ILogger<ModelCacheService> logger)
    {
        _openRouterService = openRouterService;
        _logger = logger;
    }

    private static Dictionary<PaidTier, List<CachedModel>> EmptyTierMap() =>
        PaidTierExtensions.All.ToDictionary(t => t, _ => new List<CachedModel>());

    private static Dictionary<string, PaidTier> BuildCuratedTiers()
    {
        var map = new Dictionary<string, PaidTier>(StringComparer.OrdinalIgnoreCase);
        foreach (var tier in PaidTierExtensions.All)
        {
            foreach (var id in PaidTierCatalog.CuratedFor(tier))
            {
                map.TryAdd(id, tier);
            }
        }
        return map;
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

    public List<CachedModel> GetPaidModels(PaidTier tier)
    {
        lock (_lock)
        {
            return _paidModelsByTier.TryGetValue(tier, out var models)
                ? models.ToList()
                : new List<CachedModel>();
        }
    }

    public CachedModel? GetSelectedFreeModel()
    {
        lock (_lock)
        {
            return _selectedFreeModel;
        }
    }

    public CachedModel? GetSelectedPaidModel() => GetSelectedPaidModel(PaidTier.Low);

    public CachedModel? GetSelectedPaidModel(PaidTier tier)
    {
        lock (_lock)
        {
            return _selectedPaidByTier.TryGetValue(tier, out var model) ? model : null;
        }
    }

    public List<CachedModel> GetImageModels()
    {
        lock (_lock)
        {
            return _imageModels.ToList();
        }
    }

    public CachedModel? GetSelectedImageModel()
    {
        lock (_lock)
        {
            return _selectedImageModel;
        }
    }

    public CachedModel? GetModelById(string modelId)
    {
        lock (_lock)
        {
            return _freeModels.FirstOrDefault(m => m.Id == modelId)
                   ?? _paidModels.FirstOrDefault(m => m.Id == modelId)
                   ?? _imageModels.FirstOrDefault(m => m.Id == modelId);
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

    public void SetSelectedPaidModel(string modelId) => SetSelectedPaidModel(modelId, PaidTier.Low);

    public bool SetSelectedPaidModel(string modelId, PaidTier tier)
    {
        lock (_lock)
        {
            // Only models belonging to the tier may be selected for it, otherwise a
            // "premium" selection could silently point at a cheap model.
            var model = _paidModelsByTier.TryGetValue(tier, out var models)
                ? models.FirstOrDefault(m => m.Id == modelId)
                : null;

            if (model == null)
            {
                _logger.LogWarning("Model {ModelId} is not in the {Tier} tier; selection rejected",
                    modelId, tier.ToSlug());
                return false;
            }

            _selectedPaidByTier[tier] = model;
            _logger.LogInformation("Selected {Tier} paid model set to: {ModelId}", tier.ToSlug(), modelId);
            return true;
        }
    }

    public void SetSelectedImageModel(string modelId)
    {
        lock (_lock)
        {
            var model = _imageModels.FirstOrDefault(m => m.Id == modelId);
            if (model != null)
            {
                _selectedImageModel = model;
                _logger.LogInformation("Selected image model set to: {ModelId}", modelId);
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
                    // Curation wins over the price band, so a curated model never drifts
                    // between tiers just because upstream changed its price.
                    cachedModel.IsCurated = CuratedTiers.TryGetValue(cachedModel.Id, out var curatedTier);
                    cachedModel.Tier = cachedModel.IsCurated
                        ? curatedTier
                        : PaidTierCatalog.Classify(GetTotalPrice(cachedModel));
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

            var paidByTier = BuildTierChains(paidModels);

            // Fetch image models
            var imageModelsRaw = await _openRouterService.GetImageModelsAsync(cancellationToken);
            var imageModels = imageModelsRaw
                .Where(IsValidImageModel)
                .Select(m => new CachedModel
                {
                    Id = m.Id,
                    Name = m.Name,
                    Description = m.Description,
                    ContextLength = m.ContextLength,
                    PromptPrice = m.Pricing.Prompt,
                    CompletionPrice = m.Pricing.Completion,
                    ImagePrice = m.Pricing.ImageOutput,
                    IsFree = false,
                    IsImageModel = true
                })
                .OrderBy(GetImageRankPrice)
                .Select((m, i) => { m.Rank = i + 1; return m; })
                .ToList();

            lock (_lock)
            {
                _freeModels = freeModels;
                _paidModels = paidModels;
                _paidModelsByTier = paidByTier;
                _imageModels = imageModels;
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

                // Auto-select the head of each tier's chain. An explicit admin selection is kept
                // as long as that model is still offered in the tier.
                foreach (var tier in PaidTierExtensions.All)
                {
                    var chain = paidByTier[tier];
                    var current = _selectedPaidByTier.TryGetValue(tier, out var existing) ? existing : null;

                    if (current != null && chain.Any(m => m.Id == current.Id))
                        continue;

                    if (current != null)
                    {
                        _logger.LogInformation(
                            "Previously selected {Tier} model {ModelId} is no longer offered; re-selecting",
                            tier.ToSlug(), current.Id);
                    }

                    _selectedPaidByTier[tier] = chain.FirstOrDefault();

                    if (_selectedPaidByTier[tier] is { } selected)
                    {
                        _logger.LogInformation(
                            "Auto-selected {Tier} paid model: {ModelId} ({Price} USD/Mtok, curated: {Curated})",
                            tier.ToSlug(), selected.Id, GetTotalPrice(selected) * 1_000_000m, selected.IsCurated);
                    }
                    else
                    {
                        _logger.LogWarning("No models available for the {Tier} paid tier", tier.ToSlug());
                    }
                }

                // Auto-select cheapest image model (only on first load)
                if (_selectedImageModel == null)
                {
                    _selectedImageModel = imageModels.FirstOrDefault();
                    if (_selectedImageModel != null)
                    {
                        _logger.LogInformation("Auto-selected cheapest image model: {ModelId} (price: {Price})",
                            _selectedImageModel.Id, GetTotalPrice(_selectedImageModel));
                    }
                }
            }

            _logger.LogInformation(
                "Model cache refreshed: {FreeCount} free, {PaidCount} paid ({Low} low / {Moderate} moderate / {Premium} premium), {ImageCount} image models",
                freeModels.Count, paidModels.Count,
                paidByTier[PaidTier.Low].Count, paidByTier[PaidTier.Moderate].Count,
                paidByTier[PaidTier.Premium].Count, imageModels.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh models");
        }
    }

    /// <summary>
    /// Orders each tier into the chain the chat fallback walks: curated models first in catalog
    /// order (skipping any the upstream no longer offers), then the remainder of the tier
    /// cheapest first. Entries are clones so each tier can carry its own rank.
    /// </summary>
    private Dictionary<PaidTier, List<CachedModel>> BuildTierChains(List<CachedModel> paidModels)
    {
        var result = EmptyTierMap();

        foreach (var tier in PaidTierExtensions.All)
        {
            // paidModels is already sorted cheapest-first, so this preserves price order.
            var members = paidModels.Where(m => m.Tier == tier).ToList();
            var byId = new Dictionary<string, CachedModel>(StringComparer.OrdinalIgnoreCase);
            foreach (var member in members)
            {
                byId.TryAdd(member.Id, member);
            }

            var chain = new List<CachedModel>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var curatedId in PaidTierCatalog.CuratedFor(tier))
            {
                if (byId.TryGetValue(curatedId, out var model) && seen.Add(model.Id))
                    chain.Add(model);
            }

            var curatedFound = chain.Count;

            foreach (var model in members)
            {
                if (seen.Add(model.Id))
                    chain.Add(model);
            }

            result[tier] = chain
                .Select((m, i) =>
                {
                    var clone = m.Clone();
                    clone.Rank = i + 1;
                    return clone;
                })
                .ToList();

            var curatedTotal = PaidTierCatalog.CuratedFor(tier).Count;
            if (curatedTotal > 0 && curatedFound < curatedTotal)
            {
                _logger.LogWarning(
                    "{Tier} tier: only {Found}/{Total} curated models are currently offered; the chain falls back to the price band after them",
                    tier.ToSlug(), curatedFound, curatedTotal);
            }
        }

        return result;
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

        // Exclude ":batch" variants. They are priced for asynchronous batch submission and are
        // not valid targets for the synchronous chat endpoint this gateway exposes.
        if (model.Id.EndsWith(":batch", StringComparison.OrdinalIgnoreCase))
            return false;

        // Require minimum context length
        if (model.ContextLength < 8000)
            return false;

        // Must have parseable, non-negative prices. OpenRouter uses -1 for variable.
        if (!TryPrice(model.Pricing.Prompt, out var promptPrice) ||
            !TryPrice(model.Pricing.Completion, out var completionPrice))
            return false;

        if (promptPrice < 0 || completionPrice < 0)
            return false;

        return true;
    }

    /// <summary>
    /// Image models were previously ranked with the same prompt+completion sum used
    /// for text, which let two things through.
    ///
    /// OpenRouter reports variable pricing as -1, so "openrouter/auto" summed to -2
    /// and sorted ahead of every real model in a cheapest-first list: the auto
    /// router, which is not an image model at all, was being picked as the cheapest
    /// image model. A negative price would also have produced a negative cost
    /// estimate.
    /// </summary>
    private static bool IsValidImageModel(OpenRouterModel model)
    {
        if (model.Id.Contains("/auto", StringComparison.OrdinalIgnoreCase) ||
            model.Id.Contains("router", StringComparison.OrdinalIgnoreCase))
            return false;

        if (model.Id.EndsWith(":batch", StringComparison.OrdinalIgnoreCase))
            return false;

        // Any component that will not parse, or that is negative, means the price is
        // variable rather than cheap.
        foreach (var raw in new[] { model.Pricing.Prompt, model.Pricing.Completion, model.Pricing.ImageOutput })
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            if (!TryPrice(raw, out var value) || value < 0) return false;
        }

        return true;
    }

    /// <summary>
    /// Image generation is billed on output, so rank on that. Nearly every image
    /// model reports 0 for prompt and completion, which made the old ordering
    /// effectively arbitrary across dozens of models.
    /// </summary>
    private static decimal GetImageRankPrice(CachedModel model)
    {
        if (TryPrice(model.ImagePrice, out var imageOutput) && imageOutput > 0)
            return imageOutput;

        return GetTotalPrice(model);
    }

    /// <summary>
    /// Prices arrive as API strings such as "0.0000001" or "2.39e-06". They are
    /// never formatted for the host's locale, so parse them invariantly.
    /// </summary>
    private static bool TryPrice(string? raw, out decimal value) =>
        decimal.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    private static decimal GetTotalPrice(CachedModel model)
    {
        TryPrice(model.PromptPrice, out var prompt);
        TryPrice(model.CompletionPrice, out var completion);
        return prompt + completion;
    }
}

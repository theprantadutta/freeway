using Freeway.Domain.Common;

namespace Freeway.Domain.Interfaces;

public interface IModelCacheService
{
    List<CachedModel> GetFreeModels();

    /// <summary>All paid models, cheapest first. Unchanged: this is what /models/paid serves.</summary>
    List<CachedModel> GetPaidModels();

    /// <summary>
    /// The preference chain for a single paid tier: curated models first (in catalog order,
    /// skipping any not currently offered), then the rest of the tier's price band cheapest
    /// first. This is the order the chat fallback walks.
    /// </summary>
    List<CachedModel> GetPaidModels(PaidTier tier);

    List<CachedModel> GetImageModels();
    CachedModel? GetSelectedFreeModel();

    /// <summary>Selected model for the default paid tier (Low). Unchanged behaviour.</summary>
    CachedModel? GetSelectedPaidModel();

    CachedModel? GetSelectedPaidModel(PaidTier tier);
    CachedModel? GetSelectedImageModel();
    CachedModel? GetModelById(string modelId);
    void SetSelectedFreeModel(string modelId);

    /// <summary>Sets the selected model for the default paid tier (Low). Unchanged behaviour.</summary>
    void SetSelectedPaidModel(string modelId);

    /// <summary>
    /// Sets the selected model for a tier. The model must belong to that tier's band,
    /// otherwise the selection is rejected and false is returned.
    /// </summary>
    bool SetSelectedPaidModel(string modelId, PaidTier tier);

    void SetSelectedImageModel(string modelId);
    Task RefreshModelsAsync(CancellationToken cancellationToken = default);
    DateTime? GetLastUpdated();
}

public class CachedModel
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? ContextLength { get; set; }
    public string PromptPrice { get; set; } = "0";
    public string CompletionPrice { get; set; } = "0";
    public bool IsFree { get; set; }
    public bool IsImageModel { get; set; }
    public int Rank { get; set; }

    /// <summary>Paid tier this model belongs to. Null for free and image models.</summary>
    public PaidTier? Tier { get; set; }

    /// <summary>True when the model is explicitly listed in its tier's curated catalog.</summary>
    public bool IsCurated { get; set; }

    public CachedModel Clone() => (CachedModel)MemberwiseClone();
}

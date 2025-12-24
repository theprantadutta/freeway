namespace Freeway.Domain.Interfaces;

public interface IModelCacheService
{
    List<CachedModel> GetFreeModels();
    List<CachedModel> GetPaidModels();
    CachedModel? GetSelectedFreeModel();
    CachedModel? GetSelectedPaidModel();
    CachedModel? GetModelById(string modelId);
    void SetSelectedFreeModel(string modelId);
    void SetSelectedPaidModel(string modelId);
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
    public int Rank { get; set; }
}

namespace Freeway.Application.DTOs;

public class PricingInfoDto
{
    public string Prompt { get; set; } = "0";
    public string Completion { get; set; } = "0";
}

public class ModelInfoDto
{
    public string ModelId { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? ContextLength { get; set; }
    public PricingInfoDto Pricing { get; set; } = new();
    public int? Rank { get; set; }
}

public class SelectedModelDto
{
    public string ModelId { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? ContextLength { get; set; }
    public PricingInfoDto Pricing { get; set; } = new();
}

public class ModelsListDto
{
    public List<ModelInfoDto> Models { get; set; } = new();
    public int TotalCount { get; set; }
    public DateTime? LastUpdated { get; set; }
}

public class SetModelRequest
{
    public string ModelId { get; set; } = string.Empty;
}

public class SetModelResponseDto
{
    public bool Success { get; set; }
    public string ModelId { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

// Provider model DTOs for /v1/models endpoint
public class ProviderModelDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? ContextLength { get; set; }
    public string? OwnedBy { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public class ProviderModelsListDto
{
    public List<ProviderModelDto> Data { get; set; } = new();
    public int TotalCount { get; set; }
    public int ProviderCount { get; set; }
    public Dictionary<string, DateTime?> LastUpdatedByProvider { get; set; } = new();
}

// Provider DTOs for /v1/providers endpoint
public class ProviderDto
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public bool IsFreeProvider { get; set; }
    public string DefaultModelId { get; set; } = string.Empty;
    public int ModelCount { get; set; }
    public DateTime? LastValidated { get; set; }
    public int? BenchmarkRank { get; set; }
    public double? SuccessRate { get; set; }
    public double? AvgResponseTimeMs { get; set; }
}

public class ProvidersListDto
{
    public List<ProviderDto> Providers { get; set; } = new();
    public int TotalCount { get; set; }
    public int EnabledCount { get; set; }
    public int FreeProviderCount { get; set; }
}

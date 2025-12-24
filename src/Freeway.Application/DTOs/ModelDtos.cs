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

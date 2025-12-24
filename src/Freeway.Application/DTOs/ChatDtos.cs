namespace Freeway.Application.DTOs;

public class ChatCompletionRequestDto
{
    public string Model { get; set; } = "free";
    public List<ChatMessageDto> Messages { get; set; } = new();
    public double? Temperature { get; set; }
    public int? MaxTokens { get; set; }
    public double? TopP { get; set; }
    public double? FrequencyPenalty { get; set; }
    public double? PresencePenalty { get; set; }
    public List<string>? Stop { get; set; }
    public bool Stream { get; set; }
}

public class ChatCompletionResponseDto
{
    public string Id { get; set; } = string.Empty;
    public string Object { get; set; } = "chat.completion";
    public long Created { get; set; }
    public string Model { get; set; } = string.Empty;
    public List<ChatChoiceDto> Choices { get; set; } = new();
    public UsageDto Usage { get; set; } = new();
}

public class ChatChoiceDto
{
    public int Index { get; set; }
    public ChatMessageDto Message { get; set; } = new();
    public string? FinishReason { get; set; }
}

public class UsageDto
{
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
}

public class HealthResponseDto
{
    public string Status { get; set; } = "healthy";
    public string Service { get; set; } = "freeway";
    public string Version { get; set; } = "1.0.0";
    public int FreeModelsCount { get; set; }
    public int PaidModelsCount { get; set; }
    public string? SelectedFreeModel { get; set; }
    public string? SelectedPaidModel { get; set; }
    public DateTime? LastRefresh { get; set; }
}

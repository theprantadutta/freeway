using System.Text.Json.Serialization;

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

    /// <summary>
    /// The newer name for <see cref="MaxTokens"/>, which current OpenAI SDKs send
    /// instead. Taken as max_tokens when max_tokens itself is absent.
    /// </summary>
    public int? MaxCompletionTokens { get; set; }

    /// <summary>
    /// How many choices to generate. Only 1 is supported; anything else is refused
    /// rather than quietly answered with a single choice.
    /// </summary>
    public int? N { get; set; }

    /// <summary>
    /// Reply as server-sent events (chat.completion.chunk), ending in "data: [DONE]".
    /// </summary>
    public bool Stream { get; set; }

    /// <summary>Sent as "stream_options": {"include_usage": true} to get a final usage chunk.</summary>
    public StreamOptionsDto? StreamOptions { get; set; }

    /// <summary>
    /// Serve this request from the local Claude Code subscription or not at all.
    ///
    /// The lane routing normally treats local Claude as an optimisation and falls
    /// through to a paid model whenever it cannot be used. This turns that off: the
    /// request either runs on the subscription or fails, and never quietly costs
    /// money. Sent as "use_local_only".
    /// </summary>
    public bool UseLocalOnly { get; set; }

    /// <summary>
    /// Reasoning effort, for models that do it. Sent as "reasoning", the same shape
    /// the upstream takes: {"effort": "low"}.
    ///
    /// Usually the cheapest change available on a reasoning model, because thinking
    /// is billed as output at the full rate whether or not anyone reads it.
    /// </summary>
    public ReasoningOptionsDto? Reasoning { get; set; }
}

public class StreamOptionsDto
{
    /// <summary>
    /// Send one last chunk, before [DONE], whose choices are empty and whose usage
    /// holds the request's token counts -- the only way a stream reports them.
    /// </summary>
    public bool IncludeUsage { get; set; }
}

public class ReasoningOptionsDto
{
    /// <summary>"low", "medium" or "high".</summary>
    public string? Effort { get; set; }

    /// <summary>Hides the reasoning from the reply without reducing what it costs.</summary>
    public bool? Exclude { get; set; }

    public int? MaxTokens { get; set; }

    /// <summary>Some endpoints reject false outright, failing the request.</summary>
    public bool? Enabled { get; set; }
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

/// <summary>One server-sent event of a streamed reply, in OpenAI's shape.</summary>
public class ChatCompletionChunkDto
{
    public string Id { get; set; } = string.Empty;
    public string Object { get; set; } = "chat.completion.chunk";
    public long Created { get; set; }
    public string Model { get; set; } = string.Empty;
    public List<ChatChunkChoiceDto> Choices { get; set; } = new();

    /// <summary>Only on the final usage chunk, and only when the caller asked for it.</summary>
    public UsageDto? Usage { get; set; }
}

public class ChatChunkChoiceDto
{
    public int Index { get; set; }
    public ChatDeltaDto Delta { get; set; } = new();

    /// <summary>Written as null until the last chunk, as OpenAI does.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public string? FinishReason { get; set; }
}

public class ChatDeltaDto
{
    public string? Role { get; set; }
    public string? Content { get; set; }
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
    public int ImageModelsCount { get; set; }
    public string? SelectedFreeModel { get; set; }
    public string? SelectedPaidModel { get; set; }
    public string? SelectedImageModel { get; set; }
    public DateTime? LastRefresh { get; set; }
}

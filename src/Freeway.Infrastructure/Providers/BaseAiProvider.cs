using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Freeway.Domain.Entities;
using Freeway.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Freeway.Infrastructure.Providers;

public abstract class BaseAiProvider : IAiProvider
{
    protected readonly HttpClient HttpClient;
    protected readonly ILogger Logger;
    protected readonly int CompletionTimeout;

    protected static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public abstract string Name { get; }
    public abstract string DisplayName { get; }
    public abstract bool IsFreeProvider { get; }
    public abstract string DefaultModelId { get; }

    protected abstract string ApiKey { get; }

    public bool IsEnabled => !string.IsNullOrEmpty(ApiKey);

    protected BaseAiProvider(HttpClient httpClient, ILogger logger)
    {
        HttpClient = httpClient;
        Logger = logger;
        CompletionTimeout = int.TryParse(Environment.GetEnvironmentVariable("COMPLETION_TIMEOUT_SECONDS"), out var ct) ? ct : 120;
    }

    public abstract Task<ChatCompletionResult> CreateChatCompletionAsync(
        string modelId,
        List<ChatMessage> messages,
        ChatCompletionOptions? options = null,
        CancellationToken cancellationToken = default);

    protected ChatCompletionResult CreateErrorResult(string errorMessage, int responseTimeMs, int? httpStatusCode = null)
    {
        return new ChatCompletionResult
        {
            Success = false,
            ErrorMessage = errorMessage,
            ResponseTimeMs = responseTimeMs,
            HttpStatusCode = httpStatusCode,
            ProviderName = Name
        };
    }

    protected ChatCompletionResult CreateSuccessResult(
        string id,
        string model,
        List<ChatCompletionChoice> choices,
        ChatCompletionUsage usage,
        int responseTimeMs,
        string? finishReason = null)
    {
        return new ChatCompletionResult
        {
            Id = id,
            Model = model,
            Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Choices = choices,
            Usage = usage,
            FinishReason = finishReason ?? choices.FirstOrDefault()?.FinishReason,
            Success = true,
            ResponseTimeMs = responseTimeMs,
            ProviderName = Name
        };
    }
}

using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Freeway.Domain.Entities;
using Freeway.Domain.Interfaces;
using Freeway.Infrastructure.Streaming;
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

    /// <summary>
    /// Providers without a streaming implementation of their own replay the finished
    /// completion as one delta. The caller still gets a well-formed stream; it simply
    /// arrives all at once.
    /// </summary>
    public virtual async Task<ChatCompletionStream> StreamChatCompletionAsync(
        string modelId,
        List<ChatMessage> messages,
        ChatCompletionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var result = await CreateChatCompletionAsync(modelId, messages, options, cancellationToken);
        return ChatCompletionStream.FromResult(result);
    }

    /// <summary>
    /// Streams from an OpenAI-compatible endpoint, sending the same request body the
    /// non-streaming call would with "stream" switched on.
    /// </summary>
    /// <param name="requestUsage">
    /// Adds stream_options.include_usage, without which OpenAI reports no token counts
    /// on a stream. Only for upstreams known to accept it: some reject unknown fields
    /// outright, and those send usage on their last chunk anyway.
    /// </param>
    /// <param name="label">How this upstream is named in errors, e.g. "Groq".</param>
    protected Task<ChatCompletionStream> StreamOpenAiCompatibleAsync(
        string url,
        string model,
        object request,
        string label,
        bool requestUsage,
        CancellationToken cancellationToken,
        Action<HttpRequestMessage>? configure = null)
    {
        var body = JsonSerializer.SerializeToNode(request, request.GetType(), JsonOptions)!.AsObject();
        body["stream"] = true;
        if (requestUsage)
        {
            body["stream_options"] = new JsonObject { ["include_usage"] = true };
        }

        var payload = body.ToJsonString();
        var result = UpstreamStream.NewResult(Name, model);

        HttpRequestMessage CreateRequest()
        {
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
            httpRequest.Headers.Add("Authorization", $"Bearer {ApiKey}");
            configure?.Invoke(httpRequest);
            httpRequest.Content = new StringContent(payload, Encoding.UTF8, "application/json");
            return httpRequest;
        }

        return ChatCompletionStream.OpenAsync(
            result,
            OpenAiChunkStream.ReadAsync(
                HttpClient,
                CreateRequest,
                result,
                label,
                TimeSpan.FromSeconds(CompletionTimeout),
                Logger,
                cancellationToken));
    }

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

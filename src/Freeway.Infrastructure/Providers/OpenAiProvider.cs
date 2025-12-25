using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Freeway.Domain.Entities;
using Freeway.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Freeway.Infrastructure.Providers;

public class OpenAiProvider : BaseAiProvider, IModelFetcher
{
    public string ProviderName => Name;
    public bool CanFetch => IsEnabled;
    private readonly string _apiKey;

    public override string Name => "openai";
    public override string DisplayName => "OpenAI";
    public override bool IsFreeProvider => true;
    public override string DefaultModelId => "gpt-4o-mini";
    protected override string ApiKey => _apiKey;

    public OpenAiProvider(HttpClient httpClient, ILogger<OpenAiProvider> logger) : base(httpClient, logger)
    {
        _apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "";
    }

    public override async Task<ChatCompletionResult> CreateChatCompletionAsync(
        string modelId,
        List<ChatMessage> messages,
        ChatCompletionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(CompletionTimeout));

            var model = string.IsNullOrEmpty(modelId) ? DefaultModelId : modelId;

            var request = new OpenAiRequest
            {
                Model = model,
                Messages = messages.Select(m => new OpenAiMessage { Role = m.Role, Content = m.Content }).ToList(),
                Temperature = options?.Temperature,
                MaxTokens = options?.MaxTokens,
                TopP = options?.TopP,
                FrequencyPenalty = options?.FrequencyPenalty,
                PresencePenalty = options?.PresencePenalty,
                Stop = options?.Stop,
                Stream = false
            };

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
            httpRequest.Headers.Add("Authorization", $"Bearer {_apiKey}");
            httpRequest.Content = JsonContent.Create(request, options: JsonOptions);

            var response = await HttpClient.SendAsync(httpRequest, cts.Token);
            stopwatch.Stop();

            var responseContent = await response.Content.ReadAsStringAsync(cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                Logger.LogError("OpenAI API error: {StatusCode} - {Content}", response.StatusCode, responseContent);
                return CreateErrorResult(
                    $"OpenAI API error: {response.StatusCode}",
                    (int)stopwatch.ElapsedMilliseconds,
                    (int)response.StatusCode);
            }

            var openAiResponse = JsonSerializer.Deserialize<OpenAiResponse>(responseContent, JsonOptions);

            if (openAiResponse == null)
            {
                return CreateErrorResult("Failed to parse OpenAI response", (int)stopwatch.ElapsedMilliseconds);
            }

            return CreateSuccessResult(
                id: openAiResponse.Id ?? $"openai-{Guid.NewGuid():N}",
                model: openAiResponse.Model ?? model,
                choices: openAiResponse.Choices?.Select(c => new ChatCompletionChoice
                {
                    Index = c.Index,
                    Message = new ChatMessage
                    {
                        Role = c.Message?.Role ?? "assistant",
                        Content = c.Message?.Content ?? ""
                    },
                    FinishReason = c.FinishReason
                }).ToList() ?? new List<ChatCompletionChoice>(),
                usage: new ChatCompletionUsage
                {
                    PromptTokens = openAiResponse.Usage?.PromptTokens ?? 0,
                    CompletionTokens = openAiResponse.Usage?.CompletionTokens ?? 0,
                    TotalTokens = openAiResponse.Usage?.TotalTokens ?? 0
                },
                responseTimeMs: (int)stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            return CreateErrorResult("Request timed out", (int)stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.LogError(ex, "OpenAI API request failed");
            return CreateErrorResult(ex.Message, (int)stopwatch.ElapsedMilliseconds);
        }
    }

    public async Task<ProviderModelListResult> FetchModelsAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(30));

            using var httpRequest = new HttpRequestMessage(HttpMethod.Get, "https://api.openai.com/v1/models");
            httpRequest.Headers.Add("Authorization", $"Bearer {_apiKey}");

            var response = await HttpClient.SendAsync(httpRequest, cts.Token);
            stopwatch.Stop();

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cts.Token);
                Logger.LogError("OpenAI models API error: {StatusCode} - {Content}", response.StatusCode, errorContent);
                return ProviderModelListResult.CreateError(
                    $"API returned {response.StatusCode}",
                    (int)stopwatch.ElapsedMilliseconds);
            }

            var content = await response.Content.ReadAsStringAsync(cts.Token);
            var modelsResponse = JsonSerializer.Deserialize<OpenAiModelsResponse>(content, JsonOptions);

            var models = modelsResponse?.Data?
                .Where(m => m.Id != null && IsChatModel(m.Id))
                .Select(m => new ProviderModelInfo
                {
                    Id = m.Id!,
                    Name = m.Id!,
                    ProviderName = Name,
                    OwnedBy = m.OwnedBy,
                    CreatedAt = m.Created.HasValue
                        ? DateTimeOffset.FromUnixTimeSeconds(m.Created.Value).UtcDateTime
                        : null,
                    IsAvailable = true
                })
                .ToList() ?? new List<ProviderModelInfo>();

            Logger.LogInformation("Fetched {Count} models from OpenAI", models.Count);
            return ProviderModelListResult.CreateSuccess(models, (int)stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            return ProviderModelListResult.CreateError("Request timed out", (int)stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.LogError(ex, "Failed to fetch OpenAI models");
            return ProviderModelListResult.CreateError(ex.Message, (int)stopwatch.ElapsedMilliseconds);
        }
    }

    private static bool IsChatModel(string modelId)
    {
        // Filter to only chat-capable models
        return modelId.StartsWith("gpt-", StringComparison.OrdinalIgnoreCase) ||
               modelId.StartsWith("o1", StringComparison.OrdinalIgnoreCase) ||
               modelId.StartsWith("chatgpt", StringComparison.OrdinalIgnoreCase);
    }

    // Models endpoint DTOs
    private class OpenAiModelsResponse
    {
        public List<OpenAiModelData>? Data { get; set; }
    }

    private class OpenAiModelData
    {
        public string? Id { get; set; }
        public string? OwnedBy { get; set; }
        public long? Created { get; set; }
    }

    // OpenAI-specific DTOs
    private class OpenAiRequest
    {
        public string Model { get; set; } = string.Empty;
        public List<OpenAiMessage> Messages { get; set; } = new();
        public double? Temperature { get; set; }
        public int? MaxTokens { get; set; }
        public double? TopP { get; set; }
        public double? FrequencyPenalty { get; set; }
        public double? PresencePenalty { get; set; }
        public List<string>? Stop { get; set; }
        public bool Stream { get; set; }
    }

    private class OpenAiMessage
    {
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    private class OpenAiResponse
    {
        public string? Id { get; set; }
        public string? Model { get; set; }
        public List<OpenAiChoice>? Choices { get; set; }
        public OpenAiUsage? Usage { get; set; }
    }

    private class OpenAiChoice
    {
        public int Index { get; set; }
        public OpenAiMessage? Message { get; set; }
        public string? FinishReason { get; set; }
    }

    private class OpenAiUsage
    {
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public int TotalTokens { get; set; }
    }
}

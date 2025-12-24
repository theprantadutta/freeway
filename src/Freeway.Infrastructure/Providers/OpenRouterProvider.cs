using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Freeway.Domain.Entities;
using Freeway.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Freeway.Infrastructure.Providers;

/// <summary>
/// OpenRouter provider - used as PAID fallback only when all free providers fail.
/// Uses cheapest available paid model.
/// </summary>
public class OpenRouterProvider : BaseAiProvider
{
    private readonly string _apiKey;
    private readonly IModelCacheService _modelCacheService;

    public override string Name => "openrouter";
    public override string DisplayName => "OpenRouter (Paid Fallback)";
    public override bool IsFreeProvider => false; // This is the paid fallback
    public override string DefaultModelId => ""; // Will be resolved from model cache
    protected override string ApiKey => _apiKey;

    public OpenRouterProvider(
        HttpClient httpClient,
        ILogger<OpenRouterProvider> logger,
        IModelCacheService modelCacheService) : base(httpClient, logger)
    {
        _apiKey = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY") ?? "";
        _modelCacheService = modelCacheService;
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

            // If no model specified, get the selected paid model from cache
            var model = modelId;
            if (string.IsNullOrEmpty(model))
            {
                var selectedPaidModel = _modelCacheService.GetSelectedPaidModel();
                model = selectedPaidModel?.Id ?? "openai/gpt-4o-mini"; // Default to GPT-4o-mini if not configured
            }

            var request = new OpenRouterChatRequest
            {
                Model = model,
                Messages = messages.Select(m => new OpenRouterMessage { Role = m.Role, Content = m.Content }).ToList(),
                Temperature = options?.Temperature,
                MaxTokens = options?.MaxTokens,
                TopP = options?.TopP,
                FrequencyPenalty = options?.FrequencyPenalty,
                PresencePenalty = options?.PresencePenalty,
                Stop = options?.Stop,
                Stream = false
            };

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://openrouter.ai/api/v1/chat/completions");
            httpRequest.Headers.Add("Authorization", $"Bearer {_apiKey}");
            httpRequest.Headers.Add("HTTP-Referer", "https://freeway.pranta.dev");
            httpRequest.Headers.Add("X-Title", "Freeway");
            httpRequest.Content = JsonContent.Create(request, options: JsonOptions);

            var response = await HttpClient.SendAsync(httpRequest, cts.Token);
            stopwatch.Stop();

            var responseContent = await response.Content.ReadAsStringAsync(cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                Logger.LogError("OpenRouter API error: {StatusCode} - {Content}", response.StatusCode, responseContent);
                return CreateErrorResult(
                    $"OpenRouter API error: {response.StatusCode}",
                    (int)stopwatch.ElapsedMilliseconds,
                    (int)response.StatusCode);
            }

            var openRouterResponse = JsonSerializer.Deserialize<OpenRouterChatResponse>(responseContent, JsonOptions);

            if (openRouterResponse == null)
            {
                return CreateErrorResult("Failed to parse OpenRouter response", (int)stopwatch.ElapsedMilliseconds);
            }

            return CreateSuccessResult(
                id: openRouterResponse.Id ?? $"openrouter-{Guid.NewGuid():N}",
                model: openRouterResponse.Model ?? model,
                choices: openRouterResponse.Choices?.Select(c => new ChatCompletionChoice
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
                    PromptTokens = openRouterResponse.Usage?.PromptTokens ?? 0,
                    CompletionTokens = openRouterResponse.Usage?.CompletionTokens ?? 0,
                    TotalTokens = openRouterResponse.Usage?.TotalTokens ?? 0
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
            Logger.LogError(ex, "OpenRouter API request failed");
            return CreateErrorResult(ex.Message, (int)stopwatch.ElapsedMilliseconds);
        }
    }

    // OpenRouter-specific DTOs
    private class OpenRouterChatRequest
    {
        public string Model { get; set; } = string.Empty;
        public List<OpenRouterMessage> Messages { get; set; } = new();
        public double? Temperature { get; set; }
        public int? MaxTokens { get; set; }
        public double? TopP { get; set; }
        public double? FrequencyPenalty { get; set; }
        public double? PresencePenalty { get; set; }
        public List<string>? Stop { get; set; }
        public bool Stream { get; set; }
    }

    private class OpenRouterMessage
    {
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    private class OpenRouterChatResponse
    {
        public string? Id { get; set; }
        public string? Model { get; set; }
        public long Created { get; set; }
        public List<OpenRouterChoice>? Choices { get; set; }
        public OpenRouterUsage? Usage { get; set; }
    }

    private class OpenRouterChoice
    {
        public int Index { get; set; }
        public OpenRouterMessage? Message { get; set; }
        public string? FinishReason { get; set; }
    }

    private class OpenRouterUsage
    {
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public int TotalTokens { get; set; }
    }
}

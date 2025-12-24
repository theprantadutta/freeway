using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Freeway.Domain.Entities;
using Freeway.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Freeway.Infrastructure.Services;

public class OpenRouterService : IOpenRouterService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenRouterService> _logger;
    private readonly string _apiKey;
    private readonly int _requestTimeout;
    private readonly int _completionTimeout;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public OpenRouterService(HttpClient httpClient, ILogger<OpenRouterService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY") ?? "";
        _requestTimeout = int.TryParse(Environment.GetEnvironmentVariable("REQUEST_TIMEOUT_SECONDS"), out var rt) ? rt : 30;
        _completionTimeout = int.TryParse(Environment.GetEnvironmentVariable("COMPLETION_TIMEOUT_SECONDS"), out var ct) ? ct : 120;
    }

    public async Task<List<OpenRouterModel>> GetModelsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(_requestTimeout));

            var response = await _httpClient.GetAsync("https://openrouter.ai/api/v1/models", cts.Token);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cts.Token);
            var modelsResponse = JsonSerializer.Deserialize<OpenRouterModelsResponse>(content, JsonOptions);

            return modelsResponse?.Data ?? new List<OpenRouterModel>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch models from OpenRouter");
            return new List<OpenRouterModel>();
        }
    }

    public async Task<ChatCompletionResult> CreateChatCompletionAsync(
        string modelId,
        List<ChatMessage> messages,
        ChatCompletionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(_completionTimeout));

            var request = new OpenRouterChatRequest
            {
                Model = modelId,
                Messages = messages.Select(m => new OpenRouterMessage { Role = m.Role, Content = m.Content }).ToList(),
                Temperature = options?.Temperature,
                MaxTokens = options?.MaxTokens,
                TopP = options?.TopP,
                FrequencyPenalty = options?.FrequencyPenalty,
                PresencePenalty = options?.PresencePenalty,
                Stop = options?.Stop,
                Stream = options?.Stream ?? false
            };

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://openrouter.ai/api/v1/chat/completions");
            httpRequest.Headers.Add("Authorization", $"Bearer {_apiKey}");
            httpRequest.Headers.Add("HTTP-Referer", "https://freeway.pranta.dev");
            httpRequest.Headers.Add("X-Title", "Freeway");
            httpRequest.Content = JsonContent.Create(request, options: JsonOptions);

            var response = await _httpClient.SendAsync(httpRequest, cts.Token);
            stopwatch.Stop();

            var responseContent = await response.Content.ReadAsStringAsync(cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("OpenRouter API error: {StatusCode} - {Content}", response.StatusCode, responseContent);
                return new ChatCompletionResult
                {
                    Success = false,
                    ErrorMessage = $"OpenRouter API error: {response.StatusCode}",
                    ResponseTimeMs = (int)stopwatch.ElapsedMilliseconds
                };
            }

            var completionResponse = JsonSerializer.Deserialize<OpenRouterChatResponse>(responseContent, JsonOptions);

            if (completionResponse == null)
            {
                return new ChatCompletionResult
                {
                    Success = false,
                    ErrorMessage = "Failed to parse OpenRouter response",
                    ResponseTimeMs = (int)stopwatch.ElapsedMilliseconds
                };
            }

            return new ChatCompletionResult
            {
                Id = completionResponse.Id ?? $"chatcmpl-{Guid.NewGuid():N}",
                Model = completionResponse.Model ?? modelId,
                Created = completionResponse.Created,
                Choices = completionResponse.Choices?.Select(c => new ChatCompletionChoice
                {
                    Index = c.Index,
                    Message = new ChatMessage
                    {
                        Role = c.Message?.Role ?? "assistant",
                        Content = c.Message?.Content ?? ""
                    },
                    FinishReason = c.FinishReason
                }).ToList() ?? new List<ChatCompletionChoice>(),
                Usage = new ChatCompletionUsage
                {
                    PromptTokens = completionResponse.Usage?.PromptTokens ?? 0,
                    CompletionTokens = completionResponse.Usage?.CompletionTokens ?? 0,
                    TotalTokens = completionResponse.Usage?.TotalTokens ?? 0
                },
                FinishReason = completionResponse.Choices?.FirstOrDefault()?.FinishReason,
                Success = true,
                ResponseTimeMs = (int)stopwatch.ElapsedMilliseconds
            };
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            return new ChatCompletionResult
            {
                Success = false,
                ErrorMessage = "Request timed out",
                ResponseTimeMs = (int)stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Failed to create chat completion");
            return new ChatCompletionResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                ResponseTimeMs = (int)stopwatch.ElapsedMilliseconds
            };
        }
    }

    // Internal DTOs for OpenRouter API
    private class OpenRouterModelsResponse
    {
        public List<OpenRouterModel> Data { get; set; } = new();
    }

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

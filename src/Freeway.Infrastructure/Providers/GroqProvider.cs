using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Freeway.Domain.Entities;
using Freeway.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Freeway.Infrastructure.Providers;

public class GroqProvider : BaseAiProvider
{
    private readonly string _apiKey;

    public override string Name => "groq";
    public override string DisplayName => "Groq";
    public override bool IsFreeProvider => true;
    public override string DefaultModelId => "llama-3.3-70b-versatile";
    protected override string ApiKey => _apiKey;

    public GroqProvider(HttpClient httpClient, ILogger<GroqProvider> logger) : base(httpClient, logger)
    {
        _apiKey = Environment.GetEnvironmentVariable("GROQ_API_KEY") ?? "";
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

            var request = new GroqRequest
            {
                Model = model,
                Messages = messages.Select(m => new GroqMessage { Role = m.Role, Content = m.Content }).ToList(),
                Temperature = options?.Temperature,
                MaxTokens = options?.MaxTokens,
                TopP = options?.TopP,
                FrequencyPenalty = options?.FrequencyPenalty,
                PresencePenalty = options?.PresencePenalty,
                Stop = options?.Stop,
                Stream = false
            };

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions");
            httpRequest.Headers.Add("Authorization", $"Bearer {_apiKey}");
            httpRequest.Content = JsonContent.Create(request, options: JsonOptions);

            var response = await HttpClient.SendAsync(httpRequest, cts.Token);
            stopwatch.Stop();

            var responseContent = await response.Content.ReadAsStringAsync(cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                Logger.LogError("Groq API error: {StatusCode} - {Content}", response.StatusCode, responseContent);
                return CreateErrorResult(
                    $"Groq API error: {response.StatusCode}",
                    (int)stopwatch.ElapsedMilliseconds,
                    (int)response.StatusCode);
            }

            var groqResponse = JsonSerializer.Deserialize<GroqResponse>(responseContent, JsonOptions);

            if (groqResponse == null)
            {
                return CreateErrorResult("Failed to parse Groq response", (int)stopwatch.ElapsedMilliseconds);
            }

            return CreateSuccessResult(
                id: groqResponse.Id ?? $"groq-{Guid.NewGuid():N}",
                model: groqResponse.Model ?? model,
                choices: groqResponse.Choices?.Select(c => new ChatCompletionChoice
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
                    PromptTokens = groqResponse.Usage?.PromptTokens ?? 0,
                    CompletionTokens = groqResponse.Usage?.CompletionTokens ?? 0,
                    TotalTokens = groqResponse.Usage?.TotalTokens ?? 0
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
            Logger.LogError(ex, "Groq API request failed");
            return CreateErrorResult(ex.Message, (int)stopwatch.ElapsedMilliseconds);
        }
    }

    // Groq-specific DTOs (OpenAI-compatible)
    private class GroqRequest
    {
        public string Model { get; set; } = string.Empty;
        public List<GroqMessage> Messages { get; set; } = new();
        public double? Temperature { get; set; }
        public int? MaxTokens { get; set; }
        public double? TopP { get; set; }
        public double? FrequencyPenalty { get; set; }
        public double? PresencePenalty { get; set; }
        public List<string>? Stop { get; set; }
        public bool Stream { get; set; }
    }

    private class GroqMessage
    {
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    private class GroqResponse
    {
        public string? Id { get; set; }
        public string? Model { get; set; }
        public List<GroqChoice>? Choices { get; set; }
        public GroqUsage? Usage { get; set; }
    }

    private class GroqChoice
    {
        public int Index { get; set; }
        public GroqMessage? Message { get; set; }
        public string? FinishReason { get; set; }
    }

    private class GroqUsage
    {
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public int TotalTokens { get; set; }
    }
}

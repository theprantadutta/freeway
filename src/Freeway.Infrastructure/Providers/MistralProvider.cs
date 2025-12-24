using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Freeway.Domain.Entities;
using Freeway.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Freeway.Infrastructure.Providers;

public class MistralProvider : BaseAiProvider
{
    private readonly string _apiKey;

    public override string Name => "mistral";
    public override string DisplayName => "Mistral AI";
    public override bool IsFreeProvider => true;
    public override string DefaultModelId => "mistral-small-latest";
    protected override string ApiKey => _apiKey;

    public MistralProvider(HttpClient httpClient, ILogger<MistralProvider> logger) : base(httpClient, logger)
    {
        _apiKey = Environment.GetEnvironmentVariable("MISTRAL_API_KEY") ?? "";
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

            var request = new MistralRequest
            {
                Model = model,
                Messages = messages.Select(m => new MistralMessage { Role = m.Role, Content = m.Content }).ToList(),
                Temperature = options?.Temperature,
                MaxTokens = options?.MaxTokens,
                TopP = options?.TopP,
                Stop = options?.Stop,
                Stream = false
            };

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.mistral.ai/v1/chat/completions");
            httpRequest.Headers.Add("Authorization", $"Bearer {_apiKey}");
            httpRequest.Content = JsonContent.Create(request, options: JsonOptions);

            var response = await HttpClient.SendAsync(httpRequest, cts.Token);
            stopwatch.Stop();

            var responseContent = await response.Content.ReadAsStringAsync(cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                Logger.LogError("Mistral API error: {StatusCode} - {Content}", response.StatusCode, responseContent);
                return CreateErrorResult(
                    $"Mistral API error: {response.StatusCode}",
                    (int)stopwatch.ElapsedMilliseconds,
                    (int)response.StatusCode);
            }

            var mistralResponse = JsonSerializer.Deserialize<MistralResponse>(responseContent, JsonOptions);

            if (mistralResponse == null)
            {
                return CreateErrorResult("Failed to parse Mistral response", (int)stopwatch.ElapsedMilliseconds);
            }

            return CreateSuccessResult(
                id: mistralResponse.Id ?? $"mistral-{Guid.NewGuid():N}",
                model: mistralResponse.Model ?? model,
                choices: mistralResponse.Choices?.Select(c => new ChatCompletionChoice
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
                    PromptTokens = mistralResponse.Usage?.PromptTokens ?? 0,
                    CompletionTokens = mistralResponse.Usage?.CompletionTokens ?? 0,
                    TotalTokens = mistralResponse.Usage?.TotalTokens ?? 0
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
            Logger.LogError(ex, "Mistral API request failed");
            return CreateErrorResult(ex.Message, (int)stopwatch.ElapsedMilliseconds);
        }
    }

    // Mistral-specific DTOs (OpenAI-compatible)
    private class MistralRequest
    {
        public string Model { get; set; } = string.Empty;
        public List<MistralMessage> Messages { get; set; } = new();
        public double? Temperature { get; set; }
        public int? MaxTokens { get; set; }
        public double? TopP { get; set; }
        public List<string>? Stop { get; set; }
        public bool Stream { get; set; }
    }

    private class MistralMessage
    {
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    private class MistralResponse
    {
        public string? Id { get; set; }
        public string? Model { get; set; }
        public List<MistralChoice>? Choices { get; set; }
        public MistralUsage? Usage { get; set; }
    }

    private class MistralChoice
    {
        public int Index { get; set; }
        public MistralMessage? Message { get; set; }
        public string? FinishReason { get; set; }
    }

    private class MistralUsage
    {
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public int TotalTokens { get; set; }
    }
}

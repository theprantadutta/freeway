using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Freeway.Domain.Entities;
using Freeway.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Freeway.Infrastructure.Providers;

public class CohereProvider : BaseAiProvider
{
    private readonly string _apiKey;

    public override string Name => "cohere";
    public override string DisplayName => "Cohere";
    public override bool IsFreeProvider => true;
    public override string DefaultModelId => "command-r";
    protected override string ApiKey => _apiKey;

    public CohereProvider(HttpClient httpClient, ILogger<CohereProvider> logger) : base(httpClient, logger)
    {
        _apiKey = Environment.GetEnvironmentVariable("COHERE_API_KEY") ?? "";
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

            // Convert messages to Cohere format
            var chatHistory = new List<CohereChatMessage>();
            string? systemMessage = null;
            string userMessage = "";

            foreach (var msg in messages)
            {
                if (msg.Role == "system")
                {
                    systemMessage = msg.Content;
                }
                else if (msg.Role == "user")
                {
                    // Last user message becomes the main message
                    if (!string.IsNullOrEmpty(userMessage))
                    {
                        chatHistory.Add(new CohereChatMessage { Role = "USER", Message = userMessage });
                    }
                    userMessage = msg.Content;
                }
                else if (msg.Role == "assistant")
                {
                    chatHistory.Add(new CohereChatMessage { Role = "CHATBOT", Message = msg.Content });
                }
            }

            var request = new CohereRequest
            {
                Model = model,
                Message = userMessage,
                ChatHistory = chatHistory.Count > 0 ? chatHistory : null,
                Preamble = systemMessage,
                Temperature = options?.Temperature,
                MaxTokens = options?.MaxTokens,
                P = options?.TopP,
                StopSequences = options?.Stop,
                Stream = false
            };

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.cohere.ai/v1/chat");
            httpRequest.Headers.Add("Authorization", $"Bearer {_apiKey}");
            httpRequest.Content = JsonContent.Create(request, options: JsonOptions);

            var response = await HttpClient.SendAsync(httpRequest, cts.Token);
            stopwatch.Stop();

            var responseContent = await response.Content.ReadAsStringAsync(cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                Logger.LogError("Cohere API error: {StatusCode} - {Content}", response.StatusCode, responseContent);
                return CreateErrorResult(
                    $"Cohere API error: {response.StatusCode}",
                    (int)stopwatch.ElapsedMilliseconds,
                    (int)response.StatusCode);
            }

            var cohereResponse = JsonSerializer.Deserialize<CohereResponse>(responseContent, JsonOptions);

            if (cohereResponse == null)
            {
                return CreateErrorResult("Failed to parse Cohere response", (int)stopwatch.ElapsedMilliseconds);
            }

            return CreateSuccessResult(
                id: cohereResponse.GenerationId ?? $"cohere-{Guid.NewGuid():N}",
                model: model,
                choices: new List<ChatCompletionChoice>
                {
                    new()
                    {
                        Index = 0,
                        Message = new ChatMessage
                        {
                            Role = "assistant",
                            Content = cohereResponse.Text ?? ""
                        },
                        FinishReason = MapFinishReason(cohereResponse.FinishReason)
                    }
                },
                usage: new ChatCompletionUsage
                {
                    PromptTokens = cohereResponse.Meta?.BilledUnits?.InputTokens ?? 0,
                    CompletionTokens = cohereResponse.Meta?.BilledUnits?.OutputTokens ?? 0,
                    TotalTokens = (cohereResponse.Meta?.BilledUnits?.InputTokens ?? 0) +
                                  (cohereResponse.Meta?.BilledUnits?.OutputTokens ?? 0)
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
            Logger.LogError(ex, "Cohere API request failed");
            return CreateErrorResult(ex.Message, (int)stopwatch.ElapsedMilliseconds);
        }
    }

    private static string? MapFinishReason(string? cohereReason)
    {
        return cohereReason switch
        {
            "COMPLETE" => "stop",
            "MAX_TOKENS" => "length",
            "ERROR" => "error",
            _ => cohereReason?.ToLowerInvariant()
        };
    }

    // Cohere-specific DTOs
    private class CohereRequest
    {
        public string Model { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public List<CohereChatMessage>? ChatHistory { get; set; }
        public string? Preamble { get; set; }
        public double? Temperature { get; set; }
        public int? MaxTokens { get; set; }
        public double? P { get; set; }
        public List<string>? StopSequences { get; set; }
        public bool Stream { get; set; }
    }

    private class CohereChatMessage
    {
        public string Role { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    private class CohereResponse
    {
        public string? GenerationId { get; set; }
        public string? Text { get; set; }
        public string? FinishReason { get; set; }
        public CohereMeta? Meta { get; set; }
    }

    private class CohereMeta
    {
        public CohereBilledUnits? BilledUnits { get; set; }
    }

    private class CohereBilledUnits
    {
        public int InputTokens { get; set; }
        public int OutputTokens { get; set; }
    }
}

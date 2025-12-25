using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Freeway.Domain.Entities;
using Freeway.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Freeway.Infrastructure.Providers;

public class HuggingFaceProvider : BaseAiProvider, IModelFetcher
{
    public string ProviderName => Name;
    public bool CanFetch => IsEnabled;
    private readonly string _apiKey;

    public override string Name => "huggingface";
    public override string DisplayName => "HuggingFace";
    public override bool IsFreeProvider => true;
    public override string DefaultModelId => "meta-llama/Llama-3.2-3B-Instruct";
    protected override string ApiKey => _apiKey;

    public HuggingFaceProvider(HttpClient httpClient, ILogger<HuggingFaceProvider> logger) : base(httpClient, logger)
    {
        _apiKey = Environment.GetEnvironmentVariable("HUGGINGFACE_API_KEY") ?? "";
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
            var url = $"https://api-inference.huggingface.co/models/{model}/v1/chat/completions";

            var request = new HuggingFaceRequest
            {
                Model = model,
                Messages = messages.Select(m => new HuggingFaceMessage { Role = m.Role, Content = m.Content }).ToList(),
                Temperature = options?.Temperature,
                MaxTokens = options?.MaxTokens ?? 500,
                TopP = options?.TopP,
                Stop = options?.Stop,
                Stream = false
            };

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
            httpRequest.Headers.Add("Authorization", $"Bearer {_apiKey}");
            httpRequest.Content = JsonContent.Create(request, options: JsonOptions);

            var response = await HttpClient.SendAsync(httpRequest, cts.Token);
            stopwatch.Stop();

            var responseContent = await response.Content.ReadAsStringAsync(cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                Logger.LogError("HuggingFace API error: {StatusCode} - {Content}", response.StatusCode, responseContent);
                return CreateErrorResult(
                    $"HuggingFace API error: {response.StatusCode}",
                    (int)stopwatch.ElapsedMilliseconds,
                    (int)response.StatusCode);
            }

            var hfResponse = JsonSerializer.Deserialize<HuggingFaceResponse>(responseContent, JsonOptions);

            if (hfResponse == null)
            {
                return CreateErrorResult("Failed to parse HuggingFace response", (int)stopwatch.ElapsedMilliseconds);
            }

            return CreateSuccessResult(
                id: hfResponse.Id ?? $"hf-{Guid.NewGuid():N}",
                model: hfResponse.Model ?? model,
                choices: hfResponse.Choices?.Select(c => new ChatCompletionChoice
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
                    PromptTokens = hfResponse.Usage?.PromptTokens ?? 0,
                    CompletionTokens = hfResponse.Usage?.CompletionTokens ?? 0,
                    TotalTokens = hfResponse.Usage?.TotalTokens ?? 0
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
            Logger.LogError(ex, "HuggingFace API request failed");
            return CreateErrorResult(ex.Message, (int)stopwatch.ElapsedMilliseconds);
        }
    }

    public Task<ProviderModelListResult> FetchModelsAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // HuggingFace Inference API doesn't have a simple models listing endpoint
        // Return a curated list of known-good models for chat
        var models = new List<ProviderModelInfo>
        {
            new() { Id = "meta-llama/Llama-3.2-3B-Instruct", Name = "Llama 3.2 3B Instruct", ProviderName = Name, IsAvailable = true },
            new() { Id = "meta-llama/Llama-3.1-8B-Instruct", Name = "Llama 3.1 8B Instruct", ProviderName = Name, IsAvailable = true },
            new() { Id = "mistralai/Mistral-7B-Instruct-v0.3", Name = "Mistral 7B Instruct v0.3", ProviderName = Name, IsAvailable = true },
            new() { Id = "microsoft/Phi-3-mini-4k-instruct", Name = "Phi-3 Mini 4K Instruct", ProviderName = Name, IsAvailable = true },
            new() { Id = "google/gemma-2-9b-it", Name = "Gemma 2 9B IT", ProviderName = Name, IsAvailable = true },
            new() { Id = "Qwen/Qwen2.5-7B-Instruct", Name = "Qwen 2.5 7B Instruct", ProviderName = Name, IsAvailable = true },
            new() { Id = "HuggingFaceH4/zephyr-7b-beta", Name = "Zephyr 7B Beta", ProviderName = Name, IsAvailable = true },
        };

        stopwatch.Stop();
        Logger.LogInformation("Returned {Count} curated models for HuggingFace", models.Count);
        return Task.FromResult(ProviderModelListResult.CreateSuccess(models, (int)stopwatch.ElapsedMilliseconds));
    }

    // HuggingFace-specific DTOs (OpenAI-compatible endpoint)
    private class HuggingFaceRequest
    {
        public string Model { get; set; } = string.Empty;
        public List<HuggingFaceMessage> Messages { get; set; } = new();
        public double? Temperature { get; set; }
        public int? MaxTokens { get; set; }
        public double? TopP { get; set; }
        public List<string>? Stop { get; set; }
        public bool Stream { get; set; }
    }

    private class HuggingFaceMessage
    {
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    private class HuggingFaceResponse
    {
        public string? Id { get; set; }
        public string? Model { get; set; }
        public List<HuggingFaceChoice>? Choices { get; set; }
        public HuggingFaceUsage? Usage { get; set; }
    }

    private class HuggingFaceChoice
    {
        public int Index { get; set; }
        public HuggingFaceMessage? Message { get; set; }
        public string? FinishReason { get; set; }
    }

    private class HuggingFaceUsage
    {
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public int TotalTokens { get; set; }
    }
}

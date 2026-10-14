using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Net.Http.Json;
using System.Text.Json;
using Freeway.Domain.Entities;
using Freeway.Domain.Interfaces;
using Freeway.Infrastructure.Streaming;
using Microsoft.Extensions.Logging;

namespace Freeway.Infrastructure.Providers;

public class GeminiProvider : BaseAiProvider, IModelFetcher
{
    public string ProviderName => Name;
    public bool CanFetch => IsEnabled;
    private readonly string _apiKey;

    // Gemini API uses camelCase, not snake_case
    private static readonly JsonSerializerOptions GeminiJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public override string Name => "gemini";
    public override string DisplayName => "Google Gemini";
    public override bool IsFreeProvider => true;
    public override string DefaultModelId => "gemini-2.0-flash-exp";
    protected override string ApiKey => _apiKey;

    public GeminiProvider(HttpClient httpClient, ILogger<GeminiProvider> logger) : base(httpClient, logger)
    {
        _apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? "";
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
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={_apiKey}";

            var request = BuildRequest(messages, options);

            var response = await HttpClient.PostAsJsonAsync(url, request, JsonOptions, cts.Token);
            stopwatch.Stop();

            var responseContent = await response.Content.ReadAsStringAsync(cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                Logger.LogError("Gemini API error: {StatusCode} - {Content}", response.StatusCode, responseContent);
                return CreateErrorResult(
                    $"Gemini API error: {response.StatusCode}",
                    (int)stopwatch.ElapsedMilliseconds,
                    (int)response.StatusCode);
            }

            var geminiResponse = JsonSerializer.Deserialize<GeminiResponse>(responseContent, JsonOptions);

            if (geminiResponse?.Candidates == null || geminiResponse.Candidates.Count == 0)
            {
                return CreateErrorResult("No response from Gemini", (int)stopwatch.ElapsedMilliseconds);
            }

            var candidate = geminiResponse.Candidates[0];
            var content = candidate.Content?.Parts?.FirstOrDefault()?.Text ?? "";

            return CreateSuccessResult(
                id: $"gemini-{Guid.NewGuid():N}",
                model: model,
                choices: new List<ChatCompletionChoice>
                {
                    new()
                    {
                        Index = 0,
                        Message = new ChatMessage { Role = "assistant", Content = content },
                        FinishReason = MapFinishReason(candidate.FinishReason)
                    }
                },
                usage: new ChatCompletionUsage
                {
                    PromptTokens = geminiResponse.UsageMetadata?.PromptTokenCount ?? 0,
                    CompletionTokens = geminiResponse.UsageMetadata?.CandidatesTokenCount ?? 0,
                    TotalTokens = geminiResponse.UsageMetadata?.TotalTokenCount ?? 0
                },
                responseTimeMs: (int)stopwatch.ElapsedMilliseconds,
                finishReason: MapFinishReason(candidate.FinishReason));
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            return CreateErrorResult("Request timed out", (int)stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.LogError(ex, "Gemini API request failed");
            return CreateErrorResult(ex.Message, (int)stopwatch.ElapsedMilliseconds);
        }
    }


    private static GeminiRequest BuildRequest(List<ChatMessage> messages, ChatCompletionOptions? options)
    {
        // Convert messages to Gemini format
        var contents = new List<GeminiContent>();
        string? systemInstruction = null;

        foreach (var msg in messages)
        {
            if (msg.Role == "system")
            {
                systemInstruction = msg.Content;
            }
            else
            {
                contents.Add(new GeminiContent
                {
                    Role = msg.Role == "assistant" ? "model" : "user",
                    Parts = new List<GeminiPart> { new() { Text = msg.Content } }
                });
            }
        }

        var request = new GeminiRequest
        {
            Contents = contents,
            GenerationConfig = new GeminiGenerationConfig
            {
                Temperature = options?.Temperature,
                MaxOutputTokens = options?.MaxTokens,
                TopP = options?.TopP,
                StopSequences = options?.Stop
            }
        };

        if (!string.IsNullOrEmpty(systemInstruction))
        {
            request.SystemInstruction = new GeminiContent
            {
                Parts = new List<GeminiPart> { new() { Text = systemInstruction } }
            };
        }

        return request;
    }

    public override Task<ChatCompletionStream> StreamChatCompletionAsync(
        string modelId,
        List<ChatMessage> messages,
        ChatCompletionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var model = string.IsNullOrEmpty(modelId) ? DefaultModelId : modelId;
        var result = UpstreamStream.NewResult(Name, model);

        return ChatCompletionStream.OpenAsync(
            result,
            ReadStreamAsync(model, BuildRequest(messages, options), result, cancellationToken));
    }

    /// <summary>
    /// Gemini's own streaming form: alt=sse turns streamGenerateContent into server-sent
    /// events, each one a partial GenerateContentResponse.
    /// </summary>
    private async IAsyncEnumerable<ChatCompletionDelta> ReadStreamAsync(
        string model,
        GeminiRequest request,
        ChatCompletionResult result,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var idleTimeout = TimeSpan.FromSeconds(CompletionTimeout);
        using var idle = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        idle.CancelAfter(idleTimeout);

        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:streamGenerateContent?alt=sse&key={_apiKey}";
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(request, options: JsonOptions)
        };
        using var response = await UpstreamStream.SendAsync(HttpClient, httpRequest, "Gemini", Logger, idle.Token);

        await foreach (var data in UpstreamStream.ReadSseDataAsync(response, idle, idleTimeout, idle.Token))
        {
            var delta = ParseStreamEvent(data, result);
            if (delta is not null)
            {
                yield return delta;
            }
        }
    }

    private static ChatCompletionDelta? ParseStreamEvent(string data, ChatCompletionResult result)
    {
        using var document = JsonDocument.Parse(data);
        var root = document.RootElement;

        if (root.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object)
        {
            var message = error.TryGetProperty("message", out var m) ? m.GetString() : error.GetRawText();
            int? code = error.TryGetProperty("code", out var c) && c.TryGetInt32(out var n) ? n : null;
            throw new ChatCompletionStreamException($"Gemini stream error: {message}", code);
        }

        // Each event carries the running totals, so the last one wins.
        if (root.TryGetProperty("usageMetadata", out var usage) && usage.ValueKind == JsonValueKind.Object)
        {
            var prompt = usage.TryGetProperty("promptTokenCount", out var p) ? p.GetInt32() : 0;
            var completion = usage.TryGetProperty("candidatesTokenCount", out var o) ? o.GetInt32() : 0;
            var total = usage.TryGetProperty("totalTokenCount", out var t) ? t.GetInt32() : prompt + completion;
            result.Usage = new ChatCompletionUsage
            {
                PromptTokens = prompt,
                CompletionTokens = completion,
                TotalTokens = total
            };
        }

        if (!root.TryGetProperty("candidates", out var candidates) ||
            candidates.ValueKind != JsonValueKind.Array ||
            candidates.GetArrayLength() == 0)
        {
            return null;
        }

        var candidate = candidates[0];
        var finish = candidate.TryGetProperty("finishReason", out var f) && f.ValueKind == JsonValueKind.String
            ? MapFinishReason(f.GetString())
            : null;

        var text = new StringBuilder();
        var thinking = false;
        if (candidate.TryGetProperty("content", out var content) &&
            content.TryGetProperty("parts", out var parts) &&
            parts.ValueKind == JsonValueKind.Array)
        {
            foreach (var part in parts.EnumerateArray())
            {
                if (!part.TryGetProperty("text", out var partText) || partText.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                // Thought summaries are reasoning, left out exactly as they are elsewhere.
                if (part.TryGetProperty("thought", out var thought) && thought.ValueKind == JsonValueKind.True)
                {
                    thinking = true;
                    continue;
                }

                text.Append(partText.GetString());
            }
        }

        if (text.Length > 0 || finish is not null)
        {
            return new ChatCompletionDelta { Content = text.ToString(), FinishReason = finish };
        }

        return thinking ? new ChatCompletionDelta { IsReasoning = true } : null;
    }

    private static string? MapFinishReason(string? geminiReason)
    {
        return geminiReason switch
        {
            "STOP" => "stop",
            "MAX_TOKENS" => "length",
            "SAFETY" => "content_filter",
            _ => geminiReason?.ToLowerInvariant()
        };
    }

    public async Task<ProviderModelListResult> FetchModelsAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(30));

            var url = $"https://generativelanguage.googleapis.com/v1beta/models?key={_apiKey}";
            var response = await HttpClient.GetAsync(url, cts.Token);
            stopwatch.Stop();

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cts.Token);
                Logger.LogError("Gemini models API error: {StatusCode} - {Content}", response.StatusCode, errorContent);
                return ProviderModelListResult.CreateError(
                    $"API returned {response.StatusCode}: {errorContent}",
                    (int)stopwatch.ElapsedMilliseconds,
                    (int)response.StatusCode);
            }

            var content = await response.Content.ReadAsStringAsync(cts.Token);
            var modelsResponse = JsonSerializer.Deserialize<GeminiModelsListResponse>(content, GeminiJsonOptions);

            var models = modelsResponse?.Models?
                .Where(m => m.SupportedGenerationMethods?.Contains("generateContent") == true)
                .Select(m => new ProviderModelInfo
                {
                    Id = m.Name?.Replace("models/", "") ?? "",
                    Name = m.DisplayName ?? m.Name ?? "",
                    ProviderName = Name,
                    Description = m.Description,
                    ContextLength = m.InputTokenLimit,
                    IsAvailable = true
                })
                .Where(m => !string.IsNullOrEmpty(m.Id))
                .ToList() ?? new List<ProviderModelInfo>();

            Logger.LogInformation("Fetched {Count} models from Gemini", models.Count);
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
            Logger.LogError(ex, "Failed to fetch Gemini models");
            return ProviderModelListResult.CreateError(ex.Message, (int)stopwatch.ElapsedMilliseconds);
        }
    }

    // Models list DTOs
    private class GeminiModelsListResponse
    {
        public List<GeminiModelInfo>? Models { get; set; }
    }

    private class GeminiModelInfo
    {
        public string? Name { get; set; }
        public string? DisplayName { get; set; }
        public string? Description { get; set; }
        public int? InputTokenLimit { get; set; }
        public int? OutputTokenLimit { get; set; }
        public List<string>? SupportedGenerationMethods { get; set; }
    }

    // Gemini-specific DTOs
    private class GeminiRequest
    {
        public List<GeminiContent> Contents { get; set; } = new();
        public GeminiContent? SystemInstruction { get; set; }
        public GeminiGenerationConfig? GenerationConfig { get; set; }
    }

    private class GeminiContent
    {
        public string? Role { get; set; }
        public List<GeminiPart> Parts { get; set; } = new();
    }

    private class GeminiPart
    {
        public string? Text { get; set; }
    }

    private class GeminiGenerationConfig
    {
        public double? Temperature { get; set; }
        public int? MaxOutputTokens { get; set; }
        public double? TopP { get; set; }
        public List<string>? StopSequences { get; set; }
    }

    private class GeminiResponse
    {
        public List<GeminiCandidate>? Candidates { get; set; }
        public GeminiUsageMetadata? UsageMetadata { get; set; }
    }

    private class GeminiCandidate
    {
        public GeminiContent? Content { get; set; }
        public string? FinishReason { get; set; }
    }

    private class GeminiUsageMetadata
    {
        public int PromptTokenCount { get; set; }
        public int CandidatesTokenCount { get; set; }
        public int TotalTokenCount { get; set; }
    }
}

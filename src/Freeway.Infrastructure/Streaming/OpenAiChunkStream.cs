using System.Runtime.CompilerServices;
using System.Text.Json;
using Freeway.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Freeway.Infrastructure.Streaming;

/// <summary>
/// Reads an OpenAI-format chat stream: "data: {chat.completion.chunk}" events ending
/// in "data: [DONE]". OpenRouter, Groq, Mistral, OpenAI and HuggingFace all speak it,
/// each with its own small additions, which are picked up here rather than in five
/// near-identical parsers.
/// </summary>
internal static class OpenAiChunkStream
{
    /// <param name="createRequest">
    /// Builds the request. Called once, when the stream is first read, so nothing is
    /// sent until <see cref="ChatCompletionStream.OpenAsync"/> asks for the first delta.
    /// </param>
    public static async IAsyncEnumerable<ChatCompletionDelta> ReadAsync(
        HttpClient http,
        Func<HttpRequestMessage> createRequest,
        ChatCompletionResult result,
        string label,
        TimeSpan idleTimeout,
        ILogger logger,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var idle = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        idle.CancelAfter(idleTimeout);

        using var request = createRequest();
        using var response = await UpstreamStream.SendAsync(http, request, label, logger, idle.Token);

        await foreach (var data in UpstreamStream.ReadSseDataAsync(response, idle, idleTimeout, idle.Token))
        {
            if (data == "[DONE]")
            {
                yield break;
            }

            var delta = ParseChunk(data, result, label);
            if (delta is not null)
            {
                yield return delta;
            }
        }
    }

    /// <summary>
    /// Folds one chunk's metadata into <paramref name="result"/> and returns the delta
    /// it carries, or null for a chunk with nothing to pass on -- the opening role-only
    /// chunk, or the trailing usage-only one.
    /// </summary>
    internal static ChatCompletionDelta? ParseChunk(string data, ChatCompletionResult result, string label)
    {
        using var document = JsonDocument.Parse(data);
        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        // How an upstream reports a failure after it has already said 200. OpenRouter
        // does this when the model fails mid-answer.
        if (root.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object)
        {
            var message = String(error, "message") ?? error.GetRawText();
            int? code = error.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.Number &&
                        c.TryGetInt32(out var n)
                ? n
                : null;
            throw new ChatCompletionStreamException($"{label} stream error: {message}", code);
        }

        if (String(root, "id") is { Length: > 0 } id) result.Id = id;
        if (String(root, "model") is { Length: > 0 } model) result.Model = model;
        if (root.TryGetProperty("created", out var created) && created.TryGetInt64(out var seconds) && seconds > 0)
        {
            result.Created = seconds;
        }

        // OpenRouter names the endpoint it routed to.
        if (String(root, "provider") is { Length: > 0 } upstream) result.UpstreamProvider = upstream;

        ReadUsage(root, result);

        // Groq reports usage under its own key on the last chunk.
        if (root.TryGetProperty("x_groq", out var groq) && groq.ValueKind == JsonValueKind.Object)
        {
            ReadUsage(groq, result);
        }

        if (!root.TryGetProperty("choices", out var choices) ||
            choices.ValueKind != JsonValueKind.Array ||
            choices.GetArrayLength() == 0)
        {
            return null;
        }

        var choice = choices[0];
        var finish = String(choice, "finish_reason");

        string? content = null;
        string? reasoning = null;
        if (choice.TryGetProperty("delta", out var delta) && delta.ValueKind == JsonValueKind.Object)
        {
            content = String(delta, "content");
            reasoning = String(delta, "reasoning") ?? String(delta, "reasoning_content");
        }

        if (!string.IsNullOrEmpty(content) || !string.IsNullOrEmpty(finish))
        {
            return new ChatCompletionDelta { Content = content, FinishReason = finish };
        }

        if (!string.IsNullOrEmpty(reasoning))
        {
            return new ChatCompletionDelta { IsReasoning = true };
        }

        return null;
    }

    private static void ReadUsage(JsonElement owner, ChatCompletionResult result)
    {
        if (!owner.TryGetProperty("usage", out var usage) || usage.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var prompt = Int(usage, "prompt_tokens");
        var completion = Int(usage, "completion_tokens");
        var total = Int(usage, "total_tokens");

        result.Usage = new ChatCompletionUsage
        {
            PromptTokens = prompt,
            CompletionTokens = completion,
            TotalTokens = total > 0 ? total : prompt + completion
        };

        // The billed amount, which OpenRouter includes when asked to. Preferred over
        // any estimate for the same reason it is on the non-streaming path.
        if (usage.TryGetProperty("cost", out var cost) &&
            cost.ValueKind == JsonValueKind.Number &&
            cost.TryGetDecimal(out var billed))
        {
            result.CostUsd = billed;
            result.CostSource = "provider";
        }
    }

    private static string? String(JsonElement owner, string name) =>
        owner.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int Int(JsonElement owner, string name) =>
        owner.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number &&
        value.TryGetInt32(out var n)
            ? n
            : 0;
}

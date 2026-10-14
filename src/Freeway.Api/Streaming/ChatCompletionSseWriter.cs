using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Freeway.Api.Common;
using Freeway.Application.DTOs;
using Freeway.Application.Features.Chat;
using Microsoft.AspNetCore.Http.Features;

namespace Freeway.Api.Streaming;

/// <summary>
/// Writes a stream to the caller as OpenAI-compatible server-sent events, which is
/// what every OpenAI SDK -- and everything built on one -- knows how to read:
///
///   data: {"object":"chat.completion.chunk", ..., "delta":{"role":"assistant","content":""}}
///   data: {"object":"chat.completion.chunk", ..., "delta":{"content":"Hel"}}
///   ...
///   data: {"object":"chat.completion.chunk", ..., "delta":{},"finish_reason":"stop"}
///   data: {"object":"chat.completion.chunk", "choices":[], "usage":{...}}   (if asked for)
///   data: [DONE]
///
/// "[DONE]" is sent only when the reply is complete. A failure after the stream has
/// started -- the only kind that can still happen here -- is sent as a final
/// {"error": {...}} event instead, which the SDKs raise as an exception, so a caller
/// can never mistake half an answer for a whole one.
/// </summary>
public static class ChatCompletionSseWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly byte[] Done = Encoding.UTF8.GetBytes("data: [DONE]\n\n");

    public static async Task WriteAsync(HttpContext http, ChatCompletionStreamSession session, bool includeUsage)
    {
        var response = http.Response;
        var aborted = http.RequestAborted;
        var stream = session.Stream;

        // Every chunk has to reach the caller when it is written, not when a buffer
        // somewhere fills: that is the whole of the difference streaming makes.
        http.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
        response.StatusCode = StatusCodes.Status200OK;
        response.ContentType = "text/event-stream; charset=utf-8";
        response.Headers.CacheControl = "no-cache";
        response.Headers["X-Accel-Buffering"] = "no";

        // One id and creation time for the whole reply, as OpenAI sends. By now the
        // first delta is in, so they are the upstream's own where it gave them.
        var id = stream.Result.Id;
        var created = stream.Result.Created;

        ChatCompletionChunkDto Chunk(ChatDeltaDto delta, string? finishReason) => new()
        {
            Id = id,
            Created = created,
            Model = stream.Result.Model,
            Choices = [new ChatChunkChoiceDto { Index = 0, Delta = delta, FinishReason = finishReason }]
        };

        try
        {
            await WriteEventAsync(response, Chunk(new ChatDeltaDto { Role = "assistant", Content = "" }, null), aborted);

            await foreach (var delta in stream.ReadAllAsync())
            {
                // Reasoning is not forwarded, matching the non-streaming reply; a delta
                // carrying only a finish reason is sent once, at the end.
                if (delta.IsReasoning || string.IsNullOrEmpty(delta.Content))
                {
                    continue;
                }

                await WriteEventAsync(response, Chunk(new ChatDeltaDto { Content = delta.Content }, null), aborted);
            }

            var result = stream.Result;
            if (!result.Success)
            {
                await WriteEventAsync(
                    response,
                    new { error = ApiError.Error(result.HttpStatusCode ?? 502, result.ErrorMessage ?? "The upstream failed mid-stream.") },
                    aborted);
                return;
            }

            await WriteEventAsync(response, Chunk(new ChatDeltaDto(), result.FinishReason ?? "stop"), aborted);

            if (includeUsage)
            {
                await WriteEventAsync(response, new ChatCompletionChunkDto
                {
                    Id = id,
                    Created = created,
                    Model = result.Model,
                    Choices = [],
                    Usage = new UsageDto
                    {
                        PromptTokens = result.Usage.PromptTokens,
                        CompletionTokens = result.Usage.CompletionTokens,
                        TotalTokens = result.Usage.TotalTokens
                    }
                }, aborted);
            }

            await response.Body.WriteAsync(Done, aborted);
            await response.Body.FlushAsync(aborted);
        }
        catch (Exception) when (aborted.IsCancellationRequested)
        {
            // The caller hung up. Nothing is left to send them; the request is still
            // logged, with what it produced, when the session is disposed.
        }
        finally
        {
            if (aborted.IsCancellationRequested)
            {
                stream.MarkAbandonedByCaller();
            }
        }
    }

    private static async Task WriteEventAsync<T>(HttpResponse response, T payload, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        await response.WriteAsync($"data: {json}\n\n", cancellationToken);
        await response.Body.FlushAsync(cancellationToken);
    }
}

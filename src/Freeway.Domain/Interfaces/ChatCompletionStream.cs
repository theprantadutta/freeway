using System.Diagnostics;
using System.Text;
using Freeway.Domain.Entities;

namespace Freeway.Domain.Interfaces;

/// <summary>One increment of a streamed reply.</summary>
public sealed class ChatCompletionDelta
{
    /// <summary>Text to append to the reply, or null for an event that carries none.</summary>
    public string? Content { get; init; }

    /// <summary>
    /// The upstream is thinking rather than answering. Counts as a sign of life, so it
    /// commits the stream, but its text is never forwarded -- the non-streaming reply
    /// leaves reasoning out too, and the two have to agree.
    /// </summary>
    public bool IsReasoning { get; init; }

    public string? FinishReason { get; init; }
}

/// <summary>
/// Thrown by a stream's source to fail it with a status the fallback logic can act
/// on: a 429 parks the model, a 4xx skips the provider, a 5xx is retried.
/// </summary>
public sealed class ChatCompletionStreamException : Exception
{
    public int? StatusCode { get; }

    public ChatCompletionStreamException(string message, int? statusCode = null) : base(message)
    {
        StatusCode = statusCode;
    }
}

/// <summary>
/// A completion arriving piece by piece.
///
/// The whole gateway is built on falling back -- to a retry, the next provider, the
/// next model in a tier -- and a stream can only fall back until the caller has been
/// sent something. So a stream is not handed out until its first delta has arrived:
/// <see cref="OpenAsync"/> waits for it, and anything that goes wrong before then
/// comes back as an ordinary failed <see cref="Result"/>, carrying the same status
/// code and message a non-streaming call would. Every fallback decision in the
/// gateway can therefore read a stream exactly the way it reads a result.
///
/// Once open, <see cref="Result"/> fills in as the stream is read: content from the
/// deltas, and usage, cost and the upstream's own id and model from whatever the
/// source learns along the way. It is final when reading ends, which is when the
/// request is logged.
/// </summary>
public sealed class ChatCompletionStream : IAsyncDisposable
{
    private readonly IAsyncEnumerator<ChatCompletionDelta>? _source;
    private readonly ChatCompletionDelta? _first;
    // Null for a replayed result, whose own timing is already the right one.
    private readonly Stopwatch? _clock;
    private readonly StringBuilder _content = new();

    private string? _finishReason;
    private bool _read;
    private bool _ended;
    private bool _completed;
    private bool _abandoned;

    /// <summary>
    /// The outcome. Before reading it says whether the stream opened; after reading,
    /// how it ended and what it produced.
    /// </summary>
    public ChatCompletionResult Result { get; }

    /// <summary>True when the first delta arrived and there is something to read.</summary>
    public bool Opened => _source is not null;

    /// <summary>How long the caller waited before the first delta. Null if it never came.</summary>
    public int? TimeToFirstDeltaMs { get; }

    private ChatCompletionStream(
        ChatCompletionResult result,
        IAsyncEnumerator<ChatCompletionDelta>? source,
        ChatCompletionDelta? first,
        Stopwatch? clock,
        int? timeToFirstDeltaMs)
    {
        Result = result;
        _source = source;
        _first = first;
        _clock = clock;
        TimeToFirstDeltaMs = timeToFirstDeltaMs;
    }

    /// <summary>
    /// Starts reading <paramref name="source"/> and waits for its first delta, so the
    /// caller learns whether the stream works while it can still try something else.
    /// </summary>
    /// <param name="result">
    /// Shared with the source, which writes what it learns into it: the upstream's id
    /// and model, usage, cost. Seed it with the provider name and requested model.
    /// </param>
    public static async Task<ChatCompletionStream> OpenAsync(
        ChatCompletionResult result,
        IAsyncEnumerable<ChatCompletionDelta> source)
    {
        var clock = Stopwatch.StartNew();
        var enumerator = source.GetAsyncEnumerator();
        Exception failure;

        try
        {
            if (await enumerator.MoveNextAsync())
            {
                var ttft = (int)clock.ElapsedMilliseconds;
                result.Success = true;
                result.ResponseTimeMs = ttft;
                return new ChatCompletionStream(result, enumerator, enumerator.Current, clock, ttft);
            }

            failure = new ChatCompletionStreamException("the upstream closed the stream without sending anything");
        }
        catch (Exception ex)
        {
            failure = ex;
        }

        try
        {
            await enumerator.DisposeAsync();
        }
        catch
        {
            // Already failing; the original reason is the one worth keeping.
        }

        RecordFailure(result, failure);
        result.ResponseTimeMs = (int)clock.ElapsedMilliseconds;
        return Failed(result);
    }

    /// <summary>A stream that never opened. <paramref name="failure"/> says why.</summary>
    public static ChatCompletionStream Failed(ChatCompletionResult failure)
    {
        failure.Success = false;
        return new ChatCompletionStream(failure, null, null, null, null);
    }

    /// <summary>
    /// Replays a finished completion as a stream of one delta, for backends with no
    /// streaming of their own. The caller gets the stream it asked for, just without
    /// the early first token, rather than an error for having picked a lane whose
    /// provider happens not to stream.
    /// </summary>
    public static ChatCompletionStream FromResult(ChatCompletionResult completed)
    {
        if (!completed.Success)
        {
            return Failed(completed);
        }

        var choice = completed.Choices.FirstOrDefault();
        var delta = new ChatCompletionDelta
        {
            Content = choice?.Message.Content ?? "",
            FinishReason = choice?.FinishReason ?? completed.FinishReason ?? "stop"
        };

        // The delta is handed over as the stream's first, the way OpenAsync hands over
        // the one it read, so the source behind it has nothing left to give.
        return new ChatCompletionStream(
            completed,
            Nothing().GetAsyncEnumerator(),
            delta,
            null,
            completed.ResponseTimeMs);
    }

    private static async IAsyncEnumerable<ChatCompletionDelta> Nothing()
    {
        await Task.CompletedTask;
        yield break;
    }

    /// <summary>
    /// Reads the stream to the end. A failure part-way through does not throw: it ends
    /// the enumeration and is recorded on <see cref="Result"/>, because by then the
    /// caller has been sent part of a reply and the only thing left to do is say the
    /// rest is not coming.
    /// </summary>
    public async IAsyncEnumerable<ChatCompletionDelta> ReadAllAsync()
    {
        if (_source is null || _first is null)
        {
            yield break;
        }

        if (_read)
        {
            throw new InvalidOperationException("A stream can only be read once.");
        }

        _read = true;
        var current = _first;

        try
        {
            while (true)
            {
                Accumulate(current);
                yield return current;

                bool more;
                try
                {
                    more = await _source.MoveNextAsync();
                }
                catch (Exception ex)
                {
                    RecordFailure(Result, ex);
                    break;
                }

                if (!more)
                {
                    _ended = true;
                    break;
                }

                current = _source.Current;
            }
        }
        finally
        {
            Complete();
        }
    }

    /// <summary>
    /// The caller went away before the reply finished. That is not the gateway or the
    /// upstream failing, so it is recorded as a note on a successful request rather
    /// than as an error that would drag down the success rate.
    /// </summary>
    public void MarkAbandonedByCaller()
    {
        _abandoned = true;
        if (_completed)
        {
            ApplyAbandoned();
        }
    }

    private void Accumulate(ChatCompletionDelta delta)
    {
        if (!delta.IsReasoning && delta.Content is { Length: > 0 } text)
        {
            _content.Append(text);
        }

        if (delta.FinishReason is { Length: > 0 } finish)
        {
            _finishReason = finish;
        }
    }

    private void Complete()
    {
        if (_completed) return;
        _completed = true;

        var finish = _finishReason ?? Result.FinishReason ?? (_ended && Result.Success ? "stop" : null);

        Result.Choices =
        [
            new ChatCompletionChoice
            {
                Index = 0,
                Message = new ChatMessage { Role = "assistant", Content = _content.ToString() },
                FinishReason = finish
            }
        ];
        Result.FinishReason = finish;
        if (_clock is not null)
        {
            Result.ResponseTimeMs = (int)_clock.ElapsedMilliseconds;
        }

        if (_abandoned)
        {
            ApplyAbandoned();
        }
    }

    private void ApplyAbandoned()
    {
        Result.Success = true;
        Result.HttpStatusCode = null;
        Result.ErrorMessage = "caller disconnected before the reply finished";
    }

    private static void RecordFailure(ChatCompletionResult result, Exception ex)
    {
        result.Success = false;
        (result.ErrorMessage, result.HttpStatusCode) = ex switch
        {
            ChatCompletionStreamException s => (s.Message, s.StatusCode),
            // Same wording the non-streaming providers use, so the logs read alike.
            OperationCanceledException => ("Request timed out", result.HttpStatusCode),
            _ => (ex.Message, result.HttpStatusCode)
        };
    }

    public async ValueTask DisposeAsync()
    {
        if (_source is not null)
        {
            await _source.DisposeAsync();
        }
    }
}

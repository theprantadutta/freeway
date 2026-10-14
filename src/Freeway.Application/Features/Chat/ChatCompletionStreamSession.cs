using Freeway.Domain.Interfaces;

namespace Freeway.Application.Features.Chat;

/// <summary>
/// An open stream on its way to the caller. Disposing it releases the upstream
/// connection and logs the request, once, with whatever the stream produced -- so it
/// is logged however the stream ended, including the caller leaving part-way.
/// </summary>
public sealed class ChatCompletionStreamSession : IAsyncDisposable
{
    private readonly Action _onFinished;
    private bool _finished;

    public ChatCompletionStream Stream { get; }

    public ChatCompletionStreamSession(ChatCompletionStream stream, Action onFinished)
    {
        Stream = stream;
        _onFinished = onFinished;
    }

    public async ValueTask DisposeAsync()
    {
        if (_finished) return;
        _finished = true;

        await Stream.DisposeAsync();
        _onFinished();
    }
}

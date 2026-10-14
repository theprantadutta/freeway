using Freeway.Application.Common;
using Freeway.Domain.Interfaces;
using MediatR;

namespace Freeway.Application.Features.Chat.Commands;

/// <summary>
/// Routes a streaming request and hands back the open stream.
///
/// Everything up to the first delta happens here, where it can still become an
/// ordinary error response: validation, lane resolution, local Claude, the fallback
/// chain. What comes back is either that error, with the status a non-streaming
/// request would have got, or a stream that is already producing.
/// </summary>
public class StreamChatCompletionCommandHandler
    : IRequestHandler<StreamChatCompletionCommand, Result<ChatCompletionStreamSession>>
{
    private readonly IOpenRouterService _openRouterService;
    private readonly IProviderOrchestrator _providerOrchestrator;
    private readonly ILocalClaudeService _localClaude;
    private readonly ChatRouter _router;
    private readonly ChatUsageLogger _usageLogger;

    public StreamChatCompletionCommandHandler(
        IOpenRouterService openRouterService,
        IProviderOrchestrator providerOrchestrator,
        ILocalClaudeService localClaude,
        ChatRouter router,
        ChatUsageLogger usageLogger)
    {
        _openRouterService = openRouterService;
        _providerOrchestrator = providerOrchestrator;
        _localClaude = localClaude;
        _router = router;
        _usageLogger = usageLogger;
    }

    public async Task<Result<ChatCompletionStreamSession>> Handle(
        StreamChatCompletionCommand command,
        CancellationToken cancellationToken)
    {
        var request = command.Request;
        var messages = ChatRouter.ToMessages(request);
        var options = ChatRouter.ToOptions(request);

        // The token is the caller's connection: if they hang up, the upstream call
        // stops with them rather than generating, and billing, an answer nobody reads.
        var routing = await _router.RouteAsync(request, new ChatBackends<ChatCompletionStream>(
            Free: () => _providerOrchestrator.StreamWithFallbackAsync(messages, options, cancellationToken),
            Local: priority => _localClaude.StreamAsync(messages, options, priority, cancellationToken),
            Paid: modelId => _openRouterService.StreamChatCompletionAsync(modelId, messages, options, cancellationToken),
            Outcome: stream => stream.Result));

        if (routing.Error is not null)
        {
            if (routing is { Route: { } failedRoute, Attempt: { } failed })
            {
                _usageLogger.LogInBackground(request, failedRoute, failed.Result);
                await failed.DisposeAsync();
            }

            return Result<ChatCompletionStreamSession>.Failure(routing.Error, routing.StatusCode);
        }

        var stream = routing.Attempt!;
        var route = routing.Route!;

        return Result<ChatCompletionStreamSession>.Success(new ChatCompletionStreamSession(
            stream,
            () => _usageLogger.LogInBackground(request, route, stream.Result, stream.TimeToFirstDeltaMs)));
    }
}

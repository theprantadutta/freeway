using Freeway.Application.Common;
using MediatR;

namespace Freeway.Application.Features.Chat.Commands;

/// <summary>
/// A chat completion the caller wants as a stream. Carries the ordinary request, so
/// it is validated and routed exactly as a non-streaming one would be.
/// </summary>
public record StreamChatCompletionCommand(CreateChatCompletionCommand Request)
    : IRequest<Result<ChatCompletionStreamSession>>;

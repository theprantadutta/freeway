using Freeway.Application.Common;
using Freeway.Application.DTOs;
using MediatR;

namespace Freeway.Application.Features.Chat.Commands;

public record CreateChatCompletionCommand(
    Guid ProjectId,
    string Model,
    List<ChatMessageDto> Messages,
    double? Temperature = null,
    int? MaxTokens = null,
    double? TopP = null,
    double? FrequencyPenalty = null,
    double? PresencePenalty = null,
    List<string>? Stop = null,
    bool Stream = false
) : IRequest<Result<ChatCompletionResponseDto>>;

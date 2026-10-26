using Freeway.Application.Common;
using Freeway.Application.DTOs;
using Freeway.Domain.Interfaces;
using MediatR;

namespace Freeway.Application.Features.Chat.Commands;

public class CreateChatCompletionCommandHandler : IRequestHandler<CreateChatCompletionCommand, Result<ChatCompletionResponseDto>>
{
    private readonly IOpenRouterService _openRouterService;
    private readonly IProviderOrchestrator _providerOrchestrator;
    private readonly ILocalClaudeService _localClaude;
    private readonly IAnthropicCreditService _credit;
    private readonly ChatRouter _router;
    private readonly ChatUsageLogger _usageLogger;

    public CreateChatCompletionCommandHandler(
        IOpenRouterService openRouterService,
        IProviderOrchestrator providerOrchestrator,
        ILocalClaudeService localClaude,
        IAnthropicCreditService credit,
        ChatRouter router,
        ChatUsageLogger usageLogger)
    {
        _openRouterService = openRouterService;
        _providerOrchestrator = providerOrchestrator;
        _localClaude = localClaude;
        _credit = credit;
        _router = router;
        _usageLogger = usageLogger;
    }

    public async Task<Result<ChatCompletionResponseDto>> Handle(CreateChatCompletionCommand request, CancellationToken cancellationToken)
    {
        var messages = ChatRouter.ToMessages(request);
        var options = ChatRouter.ToOptions(request);

        var routing = await _router.RouteAsync(request, new ChatBackends<ChatCompletionResult>(
            Free: () => _providerOrchestrator.ExecuteWithFallbackAsync(messages, options, cancellationToken),
            Local: priority => _localClaude.CompleteAsync(messages, options, priority, cancellationToken),
            Paid: modelId => _openRouterService.CreateChatCompletionAsync(modelId, messages, options, cancellationToken),
            Outcome: result => result,
            Credit: tier => _credit.CompleteAsync(messages, options, tier, cancellationToken)));

        if (routing is { Route: { } route, Attempt: { } attempt })
        {
            _usageLogger.LogInBackground(request, route, attempt);
        }

        if (routing.Error is not null)
        {
            return Result<ChatCompletionResponseDto>.Failure(routing.Error, routing.StatusCode);
        }

        return Result<ChatCompletionResponseDto>.Success(ToDto(routing.Attempt!));
    }

    private static ChatCompletionResponseDto ToDto(ChatCompletionResult result) => new()
    {
        Id = result.Id,
        Object = "chat.completion",
        Created = result.Created,
        Model = result.Model,
        Choices = result.Choices.Select(c => new ChatChoiceDto
        {
            Index = c.Index,
            Message = new ChatMessageDto
            {
                Role = c.Message.Role,
                Content = c.Message.Content
            },
            FinishReason = c.FinishReason
        }).ToList(),
        Usage = new UsageDto
        {
            PromptTokens = result.Usage.PromptTokens,
            CompletionTokens = result.Usage.CompletionTokens,
            TotalTokens = result.Usage.TotalTokens
        }
    };
}

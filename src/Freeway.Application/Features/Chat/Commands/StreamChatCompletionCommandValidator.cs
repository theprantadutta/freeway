using FluentValidation;

namespace Freeway.Application.Features.Chat.Commands;

public class StreamChatCompletionCommandValidator : AbstractValidator<StreamChatCompletionCommand>
{
    public StreamChatCompletionCommandValidator()
    {
        RuleFor(x => x.Request).SetValidator(new CreateChatCompletionCommandValidator());
    }
}

using FluentValidation;

namespace Freeway.Application.Features.Chat.Commands;

public class CreateChatCompletionCommandValidator : AbstractValidator<CreateChatCompletionCommand>
{
    public CreateChatCompletionCommandValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty().WithMessage("Project ID is required");

        RuleFor(x => x.Model)
            .NotEmpty().WithMessage("Model is required");

        RuleFor(x => x.Messages)
            .NotEmpty().WithMessage("Messages are required")
            .Must(m => m.Count > 0).WithMessage("At least one message is required");

        RuleForEach(x => x.Messages).ChildRules(message =>
        {
            message.RuleFor(m => m.Role)
                .NotEmpty().WithMessage("Message role is required");
            message.RuleFor(m => m.Content)
                .NotEmpty().WithMessage("Message content is required");
        });

        When(x => x.Temperature.HasValue, () =>
        {
            RuleFor(x => x.Temperature!.Value)
                .InclusiveBetween(0, 2).WithMessage("Temperature must be between 0 and 2");
        });

        When(x => x.MaxTokens.HasValue, () =>
        {
            RuleFor(x => x.MaxTokens!.Value)
                .GreaterThan(0).WithMessage("Max tokens must be greater than 0");
        });
    }
}

using FluentValidation;

namespace Freeway.Application.Features.Projects.Commands;

public class CreateProjectCommandValidator : AbstractValidator<CreateProjectCommand>
{
    public CreateProjectCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Project name is required")
            .MaximumLength(255).WithMessage("Project name must not exceed 255 characters");

        RuleFor(x => x.RateLimitPerMinute)
            .GreaterThan(0).WithMessage("Rate limit must be greater than 0")
            .LessThanOrEqualTo(10000).WithMessage("Rate limit must not exceed 10000");
    }
}

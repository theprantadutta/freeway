using FluentValidation;

namespace Freeway.Application.Features.Projects.Commands;

public class UpdateProjectCommandValidator : AbstractValidator<UpdateProjectCommand>
{
    public UpdateProjectCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Project ID is required");

        When(x => x.Name != null, () =>
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Project name cannot be empty")
                .MaximumLength(255).WithMessage("Project name must not exceed 255 characters");
        });

        When(x => x.RateLimitPerMinute.HasValue, () =>
        {
            RuleFor(x => x.RateLimitPerMinute!.Value)
                .GreaterThan(0).WithMessage("Rate limit must be greater than 0")
                .LessThanOrEqualTo(10000).WithMessage("Rate limit must not exceed 10000");
        });
    }
}

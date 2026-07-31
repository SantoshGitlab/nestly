using FluentValidation;

namespace Nestly.Application.Reviews;

public class SubmitReviewRequestValidator : AbstractValidator<SubmitReviewRequest>
{
    public SubmitReviewRequestValidator()
    {
        RuleFor(x => x.Rating).InclusiveBetween(1, 5);
        RuleFor(x => x.ReviewText).MaximumLength(2000).When(x => x.ReviewText is not null);
        RuleFor(x => x.IssueTags).MaximumLength(500).When(x => x.IssueTags is not null);
    }
}

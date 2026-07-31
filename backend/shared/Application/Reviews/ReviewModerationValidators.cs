using FluentValidation;

namespace Nestly.Application.Reviews;

/// <summary>Bounds paging and the rating-range/date-range filters so a caller cannot request an unbounded or inverted range (task 122).</summary>
public class ReviewModerationSearchRequestValidator : AbstractValidator<ReviewModerationSearchRequest>
{
    public const int MaxPageSize = 100;

    public ReviewModerationSearchRequestValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, MaxPageSize);
        RuleFor(x => x.MinRating).InclusiveBetween(1, 5).When(x => x.MinRating.HasValue);
        RuleFor(x => x.MaxRating).InclusiveBetween(1, 5).When(x => x.MaxRating.HasValue);
        RuleFor(x => x.MaxRating)
            .GreaterThanOrEqualTo(x => x.MinRating!.Value)
            .When(x => x.MinRating.HasValue && x.MaxRating.HasValue)
            .WithMessage("Maximum rating must be at or above the minimum rating.");
        RuleFor(x => x.ToUtc)
            .GreaterThanOrEqualTo(x => x.FromUtc!.Value)
            .When(x => x.FromUtc.HasValue && x.ToUtc.HasValue)
            .WithMessage("The date-to filter must be on or after the date-from filter.");
    }
}

/// <summary>A moderation note is optional but bounded (task 122) - it mirrors <see cref="Nestly.Domain.Review.ModeratorNote"/>'s column length.</summary>
public class ModerateReviewRequestValidator : AbstractValidator<ModerateReviewRequest>
{
    public ModerateReviewRequestValidator()
    {
        RuleFor(x => x.Note).MaximumLength(1000).When(x => x.Note is not null);
    }
}

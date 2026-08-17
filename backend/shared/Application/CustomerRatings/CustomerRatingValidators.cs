using FluentValidation;

namespace Nestly.Application.CustomerRatings;

public class SubmitCustomerRatingRequestValidator : AbstractValidator<SubmitCustomerRatingRequest>
{
    public SubmitCustomerRatingRequestValidator()
    {
        RuleFor(x => x.Rating).InclusiveBetween(1, 5);
        RuleFor(x => x.Note).MaximumLength(500).When(x => x.Note is not null);
    }
}

using FluentValidation;

namespace Nestly.Application.Subscriptions;

public class SubscriptionPlanCreateRequestValidator : AbstractValidator<SubscriptionPlanCreateRequest>
{
    public SubscriptionPlanCreateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
        RuleFor(x => x.BillingCycle).IsInEnum();
        RuleFor(x => x.FreeVisitsIncluded).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DiscountPercent).InclusiveBetween(0, 100);
    }
}

public class SubscriptionPlanUpdateRequestValidator : AbstractValidator<SubscriptionPlanUpdateRequest>
{
    public SubscriptionPlanUpdateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
        RuleFor(x => x.BillingCycle).IsInEnum();
        RuleFor(x => x.FreeVisitsIncluded).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DiscountPercent).InclusiveBetween(0, 100);
    }
}

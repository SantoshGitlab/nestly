using FluentValidation;

namespace Nestly.Application.Pricing;

public class PriceCalculationRequestValidator : AbstractValidator<PriceCalculationRequest>
{
    public PriceCalculationRequestValidator()
    {
        RuleFor(x => x.ServiceId).NotEmpty();
        RuleFor(x => x.CityId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleForEach(x => x.AddOns).ChildRules(addOn =>
        {
            addOn.RuleFor(a => a.AddOnId).NotEmpty();
            addOn.RuleFor(a => a.Quantity).GreaterThan(0);
        });
    }
}

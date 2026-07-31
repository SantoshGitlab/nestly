using FluentValidation;

namespace Nestly.Application.Pricing;

public class ServicePriceUpdateRequestValidator : AbstractValidator<ServicePriceUpdateRequest>
{
    public ServicePriceUpdateRequestValidator()
    {
        RuleFor(x => x.Price).GreaterThan(0);
    }
}

public class AddOnPriceUpdateRequestValidator : AbstractValidator<AddOnPriceUpdateRequest>
{
    public AddOnPriceUpdateRequestValidator()
    {
        RuleFor(x => x.Price).GreaterThan(0);
    }
}

public class CityPriceCreateRequestValidator : AbstractValidator<CityPriceCreateRequest>
{
    public CityPriceCreateRequestValidator()
    {
        RuleFor(x => x.ServiceId).NotEmpty();
        RuleFor(x => x.CityId).NotEmpty();
        RuleFor(x => x.Price).GreaterThan(0);
        RuleFor(x => x.EffectiveEndDate)
            .GreaterThanOrEqualTo(x => x.EffectiveStartDate!.Value)
            .When(x => x.EffectiveStartDate.HasValue && x.EffectiveEndDate.HasValue)
            .WithMessage("The effective end date must not be before the effective start date.");
    }
}

public class CityPriceUpdateRequestValidator : AbstractValidator<CityPriceUpdateRequest>
{
    public CityPriceUpdateRequestValidator()
    {
        RuleFor(x => x.Price).GreaterThan(0);
        RuleFor(x => x.EffectiveEndDate)
            .GreaterThanOrEqualTo(x => x.EffectiveStartDate)
            .When(x => x.EffectiveEndDate.HasValue)
            .WithMessage("The effective end date must not be before the effective start date.");
    }
}

public class PromotionalPriceCreateRequestValidator : AbstractValidator<PromotionalPriceCreateRequest>
{
    public PromotionalPriceCreateRequestValidator()
    {
        RuleFor(x => x.ServiceId).NotEmpty();
        RuleFor(x => x.DiscountedPrice).GreaterThan(0);
        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate)
            .WithMessage("The end date must not be before the start date.");
    }
}

public class PromotionalPriceUpdateRequestValidator : AbstractValidator<PromotionalPriceUpdateRequest>
{
    public PromotionalPriceUpdateRequestValidator()
    {
        RuleFor(x => x.DiscountedPrice).GreaterThan(0);
        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate)
            .WithMessage("The end date must not be before the start date.");
    }
}

public class CityPricingPolicyUpsertRequestValidator : AbstractValidator<CityPricingPolicyUpsertRequest>
{
    public CityPricingPolicyUpsertRequestValidator()
    {
        RuleFor(x => x.VisitCharge).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TaxPercentage).InclusiveBetween(0, 100);
        RuleFor(x => x.PlatformFee).GreaterThanOrEqualTo(0);
    }
}

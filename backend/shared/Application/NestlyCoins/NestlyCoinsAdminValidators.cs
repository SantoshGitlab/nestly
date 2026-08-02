using FluentValidation;

namespace Nestly.Application.NestlyCoins;

public class NestlyCoinsProgramConfigUpsertRequestValidator : AbstractValidator<NestlyCoinsProgramConfigUpsertRequest>
{
    public NestlyCoinsProgramConfigUpsertRequestValidator()
    {
        RuleFor(x => x.EarnRatePer100).GreaterThan(0);
        RuleFor(x => x.MinimumOrderAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MaxCoinsPerMonth).GreaterThan(0).When(x => x.MaxCoinsPerMonth.HasValue);
        RuleFor(x => x.ExpiryDays).GreaterThan(0);
        RuleFor(x => x.ClawbackWindowDays).GreaterThanOrEqualTo(0);
    }
}

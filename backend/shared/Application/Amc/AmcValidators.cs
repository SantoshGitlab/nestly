using FluentValidation;

namespace Nestly.Application.Amc;

public class AmcPlanCreateRequestValidator : AbstractValidator<AmcPlanCreateRequest>
{
    public AmcPlanCreateRequestValidator()
    {
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.Price).GreaterThan(0);
        RuleFor(x => x.TermMonths).GreaterThan(0).LessThanOrEqualTo(60);
        RuleFor(x => x.VisitsIncluded).GreaterThan(0).LessThanOrEqualTo(52);
    }
}

public class AmcPlanUpdateRequestValidator : AbstractValidator<AmcPlanUpdateRequest>
{
    public AmcPlanUpdateRequestValidator()
    {
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.Price).GreaterThan(0);
        RuleFor(x => x.TermMonths).GreaterThan(0).LessThanOrEqualTo(60);
        RuleFor(x => x.VisitsIncluded).GreaterThan(0).LessThanOrEqualTo(52);
    }
}

public class AmcContractPurchaseRequestValidator : AbstractValidator<AmcContractPurchaseRequest>
{
    public AmcContractPurchaseRequestValidator()
    {
        RuleFor(x => x.PlanId).NotEmpty();
        RuleFor(x => x.AssetLabel).NotEmpty().MaximumLength(150);
    }
}

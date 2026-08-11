using FluentValidation;

namespace Nestly.Application.Catalog;

public class ServiceVariantCreateRequestValidator : AbstractValidator<ServiceVariantCreateRequest>
{
    public ServiceVariantCreateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Price).GreaterThan(0);
        RuleFor(x => x.DurationMinutes).GreaterThan(0);
        RuleFor(x => x.InclusionsOverride).MaximumLength(4000);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}

public class ServiceVariantUpdateRequestValidator : AbstractValidator<ServiceVariantUpdateRequest>
{
    public ServiceVariantUpdateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Price).GreaterThan(0);
        RuleFor(x => x.DurationMinutes).GreaterThan(0);
        RuleFor(x => x.InclusionsOverride).MaximumLength(4000);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}

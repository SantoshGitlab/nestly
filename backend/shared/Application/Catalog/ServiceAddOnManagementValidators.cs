using FluentValidation;

namespace Nestly.Application.Catalog;

public class ServiceAddOnCreateRequestValidator : AbstractValidator<ServiceAddOnCreateRequest>
{
    public ServiceAddOnCreateRequestValidator()
    {
        RuleFor(x => x.ServiceId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.Price).GreaterThan(0);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}

public class ServiceAddOnUpdateRequestValidator : AbstractValidator<ServiceAddOnUpdateRequest>
{
    public ServiceAddOnUpdateRequestValidator()
    {
        RuleFor(x => x.ServiceId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.Price).GreaterThan(0);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}

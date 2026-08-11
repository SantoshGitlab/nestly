using FluentValidation;

namespace Nestly.Application.Catalog;

public class ServiceGroupCreateRequestValidator : AbstractValidator<ServiceGroupCreateRequest>
{
    public ServiceGroupCreateRequestValidator()
    {
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}

public class ServiceGroupUpdateRequestValidator : AbstractValidator<ServiceGroupUpdateRequest>
{
    public ServiceGroupUpdateRequestValidator()
    {
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}

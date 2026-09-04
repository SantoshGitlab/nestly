using FluentValidation;

namespace Nestly.Application.Catalog;

public class CategoryGroupCreateRequestValidator : AbstractValidator<CategoryGroupCreateRequest>
{
    public CategoryGroupCreateRequestValidator()
    {
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}

public class CategoryGroupUpdateRequestValidator : AbstractValidator<CategoryGroupUpdateRequest>
{
    public CategoryGroupUpdateRequestValidator()
    {
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}

using FluentValidation;

namespace Nestly.Application.Catalog;

/// <summary>Slug shape shared by every catalog entity: lowercase, hyphen-separated, URL-safe (SRS 12.5.2/12.6.2).</summary>
internal static class SlugRule
{
    public const string Pattern = "^[a-z0-9]+(-[a-z0-9]+)*$";
    public const string ErrorMessage = "Slug must be lowercase letters, numbers and hyphens only (e.g. \"deep-cleaning\").";
}

public class CategoryCreateRequestValidator : AbstractValidator<CategoryCreateRequest>
{
    public CategoryCreateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(200).Matches(SlugRule.Pattern).WithMessage(SlugRule.ErrorMessage);
        RuleFor(x => x.Description).NotNull().MaximumLength(2000);
        RuleFor(x => x.IconUrl).MaximumLength(500);
        RuleFor(x => x.BannerUrl).MaximumLength(500);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
        RuleFor(x => x.SeoTitle).MaximumLength(200);
        RuleFor(x => x.SeoMetaDescription).MaximumLength(500);
    }
}

public class CategoryUpdateRequestValidator : AbstractValidator<CategoryUpdateRequest>
{
    public CategoryUpdateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(200).Matches(SlugRule.Pattern).WithMessage(SlugRule.ErrorMessage);
        RuleFor(x => x.Description).NotNull().MaximumLength(2000);
        RuleFor(x => x.IconUrl).MaximumLength(500);
        RuleFor(x => x.BannerUrl).MaximumLength(500);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
        RuleFor(x => x.SeoTitle).MaximumLength(200);
        RuleFor(x => x.SeoMetaDescription).MaximumLength(500);
    }
}

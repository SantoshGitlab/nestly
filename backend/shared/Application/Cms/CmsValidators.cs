using FluentValidation;
using Nestly.Domain;

namespace Nestly.Application.Cms;

// ---------------------------------------------------------------------
// Media
// ---------------------------------------------------------------------

public class CmsMediaCreateRequestValidator : AbstractValidator<CmsMediaCreateRequest>
{
    public CmsMediaCreateRequestValidator()
    {
        RuleFor(x => x.Url).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.AltText).MaximumLength(300);
        RuleFor(x => x.MediaType).IsInEnum();
    }
}

public class CmsMediaUpdateRequestValidator : AbstractValidator<CmsMediaUpdateRequest>
{
    public CmsMediaUpdateRequestValidator()
    {
        RuleFor(x => x.Url).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.AltText).MaximumLength(300);
        RuleFor(x => x.MediaType).IsInEnum();
    }
}

// ---------------------------------------------------------------------
// Pages
// ---------------------------------------------------------------------

public class CmsPageCreateRequestValidator : AbstractValidator<CmsPageCreateRequest>
{
    public CmsPageCreateRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(200)
            .Matches("^[a-z0-9]+(-[a-z0-9]+)*$")
            .WithMessage("Slug must contain only lowercase letters, numbers, and hyphens.");
        RuleFor(x => x.Body).NotEmpty();
        RuleFor(x => x.SeoTitle).MaximumLength(200);
        RuleFor(x => x.SeoDescription).MaximumLength(500);
        RuleFor(x => x.SeoKeywords).MaximumLength(300);
        RuleFor(x => x)
            .Must(x => !x.PublishStartUtc.HasValue || !x.PublishEndUtc.HasValue || x.PublishEndUtc.Value > x.PublishStartUtc.Value)
            .WithName("PublishEndUtc")
            .WithMessage("Publish end date must be after the publish start date.");
    }
}

public class CmsPageUpdateRequestValidator : AbstractValidator<CmsPageUpdateRequest>
{
    public CmsPageUpdateRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(200)
            .Matches("^[a-z0-9]+(-[a-z0-9]+)*$")
            .WithMessage("Slug must contain only lowercase letters, numbers, and hyphens.");
        RuleFor(x => x.Body).NotEmpty();
        RuleFor(x => x.SeoTitle).MaximumLength(200);
        RuleFor(x => x.SeoDescription).MaximumLength(500);
        RuleFor(x => x.SeoKeywords).MaximumLength(300);
        RuleFor(x => x)
            .Must(x => !x.PublishStartUtc.HasValue || !x.PublishEndUtc.HasValue || x.PublishEndUtc.Value > x.PublishStartUtc.Value)
            .WithName("PublishEndUtc")
            .WithMessage("Publish end date must be after the publish start date.");
    }
}

public class CmsPageAdminSearchRequestValidator : AbstractValidator<CmsPageAdminSearchRequest>
{
    public CmsPageAdminSearchRequestValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

// ---------------------------------------------------------------------
// Banners
// ---------------------------------------------------------------------

public class BannerCreateRequestValidator : AbstractValidator<BannerCreateRequest>
{
    public BannerCreateRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Subtitle).MaximumLength(300);
        RuleFor(x => x.MediaId).NotEqual(Guid.Empty).WithMessage("A media asset is required.");
        RuleFor(x => x.LinkUrl).MaximumLength(2000);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CategoryId).NotNull()
            .When(x => x.Placement == CmsPlacement.CategoryPage)
            .WithMessage("A category is required when placement is CategoryPage.");
        RuleFor(x => x)
            .Must(x => !x.PublishStartUtc.HasValue || !x.PublishEndUtc.HasValue || x.PublishEndUtc.Value > x.PublishStartUtc.Value)
            .WithName("PublishEndUtc")
            .WithMessage("Publish end date must be after the publish start date.");
    }
}

public class BannerUpdateRequestValidator : AbstractValidator<BannerUpdateRequest>
{
    public BannerUpdateRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Subtitle).MaximumLength(300);
        RuleFor(x => x.MediaId).NotEqual(Guid.Empty).WithMessage("A media asset is required.");
        RuleFor(x => x.LinkUrl).MaximumLength(2000);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CategoryId).NotNull()
            .When(x => x.Placement == CmsPlacement.CategoryPage)
            .WithMessage("A category is required when placement is CategoryPage.");
        RuleFor(x => x)
            .Must(x => !x.PublishStartUtc.HasValue || !x.PublishEndUtc.HasValue || x.PublishEndUtc.Value > x.PublishStartUtc.Value)
            .WithName("PublishEndUtc")
            .WithMessage("Publish end date must be after the publish start date.");
    }
}

public class BannerAdminSearchRequestValidator : AbstractValidator<BannerAdminSearchRequest>
{
    public BannerAdminSearchRequestValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

// ---------------------------------------------------------------------
// FAQs
// ---------------------------------------------------------------------

public class CmsFaqCreateRequestValidator : AbstractValidator<CmsFaqCreateRequest>
{
    public CmsFaqCreateRequestValidator()
    {
        RuleFor(x => x.Question).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Answer).NotEmpty();
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
        RuleFor(x => x)
            .Must(x => !x.PublishStartUtc.HasValue || !x.PublishEndUtc.HasValue || x.PublishEndUtc.Value > x.PublishStartUtc.Value)
            .WithName("PublishEndUtc")
            .WithMessage("Publish end date must be after the publish start date.");
    }
}

public class CmsFaqUpdateRequestValidator : AbstractValidator<CmsFaqUpdateRequest>
{
    public CmsFaqUpdateRequestValidator()
    {
        RuleFor(x => x.Question).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Answer).NotEmpty();
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
        RuleFor(x => x)
            .Must(x => !x.PublishStartUtc.HasValue || !x.PublishEndUtc.HasValue || x.PublishEndUtc.Value > x.PublishStartUtc.Value)
            .WithName("PublishEndUtc")
            .WithMessage("Publish end date must be after the publish start date.");
    }
}

public class CmsFaqAdminSearchRequestValidator : AbstractValidator<CmsFaqAdminSearchRequest>
{
    public CmsFaqAdminSearchRequestValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

using FluentValidation;
using Nestly.Domain;

namespace Nestly.Application.Catalog;

public class ServiceCreateRequestValidator : AbstractValidator<ServiceCreateRequest>
{
    public ServiceCreateRequestValidator()
    {
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(200).Matches(SlugRule.Pattern).WithMessage(SlugRule.ErrorMessage);
        RuleFor(x => x.Description).NotNull().MaximumLength(2000);
        RuleFor(x => x.ShortDescription).MaximumLength(500);
        RuleFor(x => x.Price).GreaterThan(0);
        RuleFor(x => x.Inclusions).NotNull().MaximumLength(4000);
        RuleFor(x => x.Exclusions).NotNull().MaximumLength(4000);
        RuleFor(x => x.CancellationPolicy).MaximumLength(2000);
        RuleFor(x => x.ReschedulePolicy).MaximumLength(2000);
        RuleFor(x => x.DurationMinutes).GreaterThan(0);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
        RuleFor(x => x.SeoTitle).MaximumLength(200);
        RuleFor(x => x.SeoMetaDescription).MaximumLength(500);
        RuleFor(x => x.PricingType).Must(BeAValidPricingType)
            .WithMessage($"Pricing type must be one of: {string.Join(", ", Enum.GetNames<ServicePricingType>())}.");
    }

    private static bool BeAValidPricingType(string value) => Enum.TryParse<ServicePricingType>(value, out _);
}

public class ServiceUpdateRequestValidator : AbstractValidator<ServiceUpdateRequest>
{
    public ServiceUpdateRequestValidator()
    {
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(200).Matches(SlugRule.Pattern).WithMessage(SlugRule.ErrorMessage);
        RuleFor(x => x.Description).NotNull().MaximumLength(2000);
        RuleFor(x => x.ShortDescription).MaximumLength(500);
        RuleFor(x => x.Price).GreaterThan(0);
        RuleFor(x => x.Inclusions).NotNull().MaximumLength(4000);
        RuleFor(x => x.Exclusions).NotNull().MaximumLength(4000);
        RuleFor(x => x.CancellationPolicy).MaximumLength(2000);
        RuleFor(x => x.ReschedulePolicy).MaximumLength(2000);
        RuleFor(x => x.DurationMinutes).GreaterThan(0);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
        RuleFor(x => x.SeoTitle).MaximumLength(200);
        RuleFor(x => x.SeoMetaDescription).MaximumLength(500);
        RuleFor(x => x.PricingType).Must(BeAValidPricingType)
            .WithMessage($"Pricing type must be one of: {string.Join(", ", Enum.GetNames<ServicePricingType>())}.");
    }

    private static bool BeAValidPricingType(string value) => Enum.TryParse<ServicePricingType>(value, out _);
}

public class ServiceMediaCreateRequestValidator : AbstractValidator<ServiceMediaCreateRequest>
{
    public ServiceMediaCreateRequestValidator()
    {
        RuleFor(x => x.Url).NotEmpty().MaximumLength(1000);
    }
}

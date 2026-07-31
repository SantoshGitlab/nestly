using FluentValidation;
using Nestly.Domain;

namespace Nestly.Application.Coupons;

public class CouponCreateRequestValidator : AbstractValidator<CouponCreateRequest>
{
    public CouponCreateRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Description).MaximumLength(300);

        RuleFor(x => x.DiscountValue).GreaterThan(0);
        RuleFor(x => x.DiscountValue)
            .LessThanOrEqualTo(100)
            .When(x => x.DiscountType == CouponDiscountType.Percentage)
            .WithMessage("A percentage discount cannot exceed 100.");

        RuleFor(x => x.MaxDiscountAmount).GreaterThan(0).When(x => x.MaxDiscountAmount.HasValue);
        RuleFor(x => x.MinOrderAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ValidToUtc).GreaterThan(x => x.ValidFromUtc)
            .WithMessage("Coupon valid-to date must be after the valid-from date.");

        RuleFor(x => x.UsageLimitTotal).GreaterThan(0).When(x => x.UsageLimitTotal.HasValue);
        RuleFor(x => x.UsageLimitPerCustomer).GreaterThan(0).When(x => x.UsageLimitPerCustomer.HasValue);
    }
}

public class CouponUpdateRequestValidator : AbstractValidator<CouponUpdateRequest>
{
    public CouponUpdateRequestValidator()
    {
        RuleFor(x => x.Description).MaximumLength(300);

        RuleFor(x => x.DiscountValue).GreaterThan(0);
        RuleFor(x => x.DiscountValue)
            .LessThanOrEqualTo(100)
            .When(x => x.DiscountType == CouponDiscountType.Percentage)
            .WithMessage("A percentage discount cannot exceed 100.");

        RuleFor(x => x.MaxDiscountAmount).GreaterThan(0).When(x => x.MaxDiscountAmount.HasValue);
        RuleFor(x => x.MinOrderAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ValidToUtc).GreaterThan(x => x.ValidFromUtc)
            .WithMessage("Coupon valid-to date must be after the valid-from date.");

        RuleFor(x => x.UsageLimitTotal).GreaterThan(0).When(x => x.UsageLimitTotal.HasValue);
        RuleFor(x => x.UsageLimitPerCustomer).GreaterThan(0).When(x => x.UsageLimitPerCustomer.HasValue);
    }
}

public class CouponAdminSearchRequestValidator : AbstractValidator<CouponAdminSearchRequest>
{
    public CouponAdminSearchRequestValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

namespace Nestly.Application.Coupons;

/// <summary>A validated coupon's effect on an order (SRS 11.10.3 "show discount value and success message").</summary>
public record CouponSummaryResponse(Guid CouponId, string Code, string? Description, decimal DiscountAmount);

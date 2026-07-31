using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>
/// A discount code (SRS 23.4 coupon, SRS 11.10, 14.2). Owns the rules that
/// are pure functions of its own fields plus a single order amount -
/// validity window (task 72b), discount calculation with cap enforcement
/// (SRS "coupon discount must not exceed cap"). Checks that require reading
/// other aggregates (global/per-customer usage counts, category and
/// first/repeat-booking applicability - task 72c/72d) live in
/// <c>ICouponService</c> instead, since a value read from another table has
/// no business being decided inside this entity.
///
/// <see cref="RedemptionCount"/> is a denormalized counter incremented
/// atomically at the database layer (see <c>ICouponRepository.TryRedeemAsync</c>)
/// so the global usage cap holds under concurrent bookings without requiring
/// serializable transactions.
/// </summary>
public class Coupon : Entity<Guid>
{
    public string Code { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public CouponDiscountType DiscountType { get; private set; }

    public decimal DiscountValue { get; private set; }

    /// <summary>Caps a percentage discount's absolute rupee value (SRS 11.10.2 "max discount amount"). Null = uncapped.</summary>
    public decimal? MaxDiscountAmount { get; private set; }

    public decimal MinOrderAmount { get; private set; }

    public DateTime ValidFromUtc { get; private set; }

    public DateTime ValidToUtc { get; private set; }

    public bool IsActive { get; private set; }

    /// <summary>Overall campaign usage cap (SRS 11.10.2). Null = unlimited.</summary>
    public int? UsageLimitTotal { get; private set; }

    /// <summary>Per-customer usage cap (SRS 11.10.2). Null = unlimited; defaults to 1 (single use per customer) at construction.</summary>
    public int? UsageLimitPerCustomer { get; private set; }

    public int RedemptionCount { get; private set; }

    /// <summary>When set, this coupon only applies to services in this category (SRS 11.10.2 "category / service / city applicability").</summary>
    public Guid? ApplicableCategoryId { get; private set; }

    public CouponCustomerSegment CustomerSegment { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    protected Coupon() { }

    public Coupon(
        Guid id,
        string code,
        string? description,
        CouponDiscountType discountType,
        decimal discountValue,
        decimal? maxDiscountAmount,
        decimal minOrderAmount,
        DateTime validFromUtc,
        DateTime validToUtc,
        int? usageLimitTotal,
        int? usageLimitPerCustomer,
        Guid? applicableCategoryId,
        CouponCustomerSegment customerSegment)
        : base(id)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Coupon code is required.", nameof(code));
        }

        if (discountValue <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(discountValue), "Discount value must be positive.");
        }

        if (discountType == CouponDiscountType.Percentage && discountValue > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(discountValue), "A percentage discount cannot exceed 100.");
        }

        if (validToUtc <= validFromUtc)
        {
            throw new ArgumentException("Coupon valid-to date must be after the valid-from date.", nameof(validToUtc));
        }

        Code = code.Trim().ToUpperInvariant();
        Description = description;
        DiscountType = discountType;
        DiscountValue = discountValue;
        MaxDiscountAmount = maxDiscountAmount;
        MinOrderAmount = minOrderAmount < 0 ? 0 : minOrderAmount;
        ValidFromUtc = validFromUtc;
        ValidToUtc = validToUtc;
        IsActive = true;
        UsageLimitTotal = usageLimitTotal;
        UsageLimitPerCustomer = usageLimitPerCustomer;
        RedemptionCount = 0;
        ApplicableCategoryId = applicableCategoryId;
        CustomerSegment = customerSegment;
        CreatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Validity window check (task 72b) - active flag plus the [ValidFrom, ValidTo] date range.</summary>
    public bool IsWithinValidityWindow(DateTime nowUtc) =>
        IsActive && nowUtc >= ValidFromUtc && nowUtc <= ValidToUtc;

    /// <summary>
    /// Computes the discount for an order of <paramref name="orderAmount"/>,
    /// or a business error if the order does not meet the minimum. The
    /// result is clamped so it can never exceed either the configured cap or
    /// the order amount itself (a coupon can never make a booking free by
    /// accident of rounding).
    /// </summary>
    public bool TryCalculateDiscount(decimal orderAmount, out decimal discountAmount)
    {
        discountAmount = 0m;

        if (orderAmount < MinOrderAmount)
        {
            return false;
        }

        decimal raw = DiscountType == CouponDiscountType.Percentage
            ? Math.Round(orderAmount * DiscountValue / 100m, 2)
            : DiscountValue;

        if (MaxDiscountAmount.HasValue && raw > MaxDiscountAmount.Value)
        {
            raw = MaxDiscountAmount.Value;
        }

        discountAmount = Math.Min(raw, orderAmount);
        return true;
    }
}

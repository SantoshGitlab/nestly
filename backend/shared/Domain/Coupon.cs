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

    /// <summary>
    /// When set, only this specific customer may redeem this coupon -
    /// checked by <c>CouponService.ValidateAsync</c> alongside every other
    /// applicability rule. Null (the default, and the only state every
    /// admin-created coupon has today) means unrestricted: applicability is
    /// governed entirely by <see cref="CustomerSegment"/>/<see cref="UsageLimitPerCustomer"/>
    /// as before. Added for referral-issued single-recipient coupon rewards
    /// (REFERRAL.md, task 165) - before this field existed, "single-use for
    /// customer X" could only be approximated via a global code plus
    /// <see cref="UsageLimitPerCustomer"/>=1, which does not stop a
    /// *different* customer who somehow obtains the code from redeeming it
    /// first. Not part of the constructor deliberately: every existing
    /// admin-coupon call site (<c>CouponManagementService.CreateAsync</c>)
    /// creates an unrestricted campaign coupon and should never need to
    /// change to accommodate this - see <see cref="RestrictToCustomer"/>.
    /// </summary>
    public Guid? RestrictedToCustomerId { get; private set; }

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
    /// Restricts this coupon to exactly one customer (REFERRAL.md task 165).
    /// Set-once, called immediately after construction by whichever service
    /// issues a single-recipient coupon - never exposed as an admin edit,
    /// since a campaign coupon becoming customer-restricted (or vice versa)
    /// after the fact isn't a real scenario this platform has.
    /// </summary>
    public void RestrictToCustomer(Guid customerId)
    {
        if (RestrictedToCustomerId is not null)
        {
            throw new InvalidOperationException("Coupon is already restricted to a customer.");
        }

        RestrictedToCustomerId = customerId;
    }

    /// <summary>
    /// Applies admin edits to every mutable rule dimension (SRS 12.12.1,
    /// task 118). <see cref="Code"/> is deliberately excluded - it is the
    /// identifier customers type in at checkout and other rows (e.g.
    /// <see cref="CouponRedemption"/>) key off the coupon's id, not its code,
    /// so nothing downstream breaks if it changed, but silently swapping a
    /// code an admin has already shared/printed out from under them is a
    /// support-desk problem this entity has no business creating. Same
    /// validation as the constructor - an edit must leave the coupon in a
    /// state the constructor itself would have accepted.
    /// </summary>
    public void Update(
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
    {
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

        Description = description;
        DiscountType = discountType;
        DiscountValue = discountValue;
        MaxDiscountAmount = maxDiscountAmount;
        MinOrderAmount = minOrderAmount < 0 ? 0 : minOrderAmount;
        ValidFromUtc = validFromUtc;
        ValidToUtc = validToUtc;
        UsageLimitTotal = usageLimitTotal;
        UsageLimitPerCustomer = usageLimitPerCustomer;
        ApplicableCategoryId = applicableCategoryId;
        CustomerSegment = customerSegment;
    }

    /// <summary>Re-enables a coupon for redemption (SRS 12.12.1 "Active status").</summary>
    public void Activate() => IsActive = true;

    /// <summary>Suspends a coupon without deleting it - existing redemptions are untouched, no further ones can be reserved (SRS 12.12.1 "Active status").</summary>
    public void Deactivate() => IsActive = false;

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
            // Explicit MidpointRounding (task 260) - see CommissionCalculator.
            ? Math.Round(orderAmount * DiscountValue / 100m, 2, MidpointRounding.ToEven)
            : DiscountValue;

        if (MaxDiscountAmount.HasValue && raw > MaxDiscountAmount.Value)
        {
            raw = MaxDiscountAmount.Value;
        }

        discountAmount = Math.Min(raw, orderAmount);
        return true;
    }
}

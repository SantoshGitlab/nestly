using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>
/// One redemption of a <see cref="Coupon"/>, linked to the customer and
/// booking it was used on (SRS 23.4 coupon_redemption, SRS 14.2 "coupon usage
/// must be linked to customer and booking"). A booking can carry at most one
/// redemption (enforced by a unique index on BookingId) since
/// <see cref="Booking"/> snapshots a single coupon code.
/// </summary>
public class CouponRedemption : Entity<Guid>
{
    public Guid CouponId { get; private set; }

    public Guid CustomerId { get; private set; }

    public Guid BookingId { get; private set; }

    public decimal DiscountAmount { get; private set; }

    public DateTime RedeemedAtUtc { get; private set; }

    protected CouponRedemption() { }

    public CouponRedemption(Guid id, Guid couponId, Guid customerId, Guid bookingId, decimal discountAmount)
        : base(id)
    {
        if (discountAmount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(discountAmount), "Discount amount must be positive.");
        }

        CouponId = couponId;
        CustomerId = customerId;
        BookingId = bookingId;
        DiscountAmount = discountAmount;
        RedeemedAtUtc = DateTime.UtcNow;
    }
}

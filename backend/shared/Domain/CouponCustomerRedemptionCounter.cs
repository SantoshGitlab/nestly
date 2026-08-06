using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>
/// Live per-customer redemption count against a <see cref="Coupon"/>'s
/// <see cref="Coupon.UsageLimitPerCustomer"/> cap (SRS 14.2, NESTLY-009).
/// One row per (<see cref="CouponId"/>, <see cref="CustomerId"/>) pair,
/// created lazily on that customer's first redemption of that coupon.
///
/// This exists because <see cref="CouponRedemption"/> cannot serve as the
/// per-customer reservation record: it carries a required foreign key to the
/// booking, so it cannot be written until the booking has been persisted -
/// which is *after* the usage cap must already have been claimed. Counting
/// redemption rows at reservation time therefore sees zero for every request
/// in a concurrent batch, since none of them has committed its row yet, and
/// a single-use coupon can be redeemed twice. This counter is incremented in
/// the same conditional UPDATE that enforces the cap, before any booking
/// exists, which closes that window.
///
/// Reservation must always go through
/// <c>CouponRepository.TryReserveRedemptionAsync</c>, which performs a single
/// atomic conditional UPDATE - never read the count and write a decision
/// separately, or two concurrent bookings racing to spend the same customer's
/// last allowance can both win. Same shape as
/// <see cref="SlotBookingCounter"/>, which solves the identical problem for
/// per-slot capacity.
/// </summary>
public class CouponCustomerRedemptionCounter : Entity<Guid>
{
    public Guid CouponId { get; private set; }
    public Guid CustomerId { get; private set; }
    public int ReservedCount { get; private set; }

    protected CouponCustomerRedemptionCounter() { }

    public CouponCustomerRedemptionCounter(Guid id, Guid couponId, Guid customerId, int reservedCount) : base(id)
    {
        CouponId = couponId;
        CustomerId = customerId;
        ReservedCount = reservedCount;
    }
}

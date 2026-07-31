using Nestly.Domain;

namespace Nestly.Application.Coupons;

public interface ICouponRedemptionRepository
{
    Task AddAsync(CouponRedemption redemption);

    /// <summary>Per-customer usage count for a coupon (task 72c per-customer cap).</summary>
    Task<int> CountByCouponAndCustomerAsync(Guid couponId, Guid customerId);

    /// <summary>Every redemption a customer has made, most recent first (SRS 12.4.2 "Coupons used", task 101b).</summary>
    Task<IReadOnlyList<CouponRedemption>> ListByCustomerAsync(Guid customerId);
}

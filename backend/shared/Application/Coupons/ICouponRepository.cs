using Nestly.Domain;

namespace Nestly.Application.Coupons;

public interface ICouponRepository
{
    Task<Coupon?> GetByIdAsync(Guid id);

    /// <summary>Codes are normalized to uppercase at construction (see <see cref="Coupon"/>) - lookups must match that normalization.</summary>
    Task<Coupon?> GetByCodeAsync(string code);

    /// <summary>
    /// Atomically increments the coupon's redemption counter, but only if it
    /// is still under its overall usage cap (or the cap is unlimited) - a
    /// single conditional UPDATE, not a read-then-write, so the global usage
    /// cap (task 72c) holds even under concurrent bookings racing for the
    /// last redemption. Returns false (no state change) if the cap had
    /// already been reached.
    /// </summary>
    Task<bool> TryReserveRedemptionAsync(Guid couponId);
}

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

    // ---- Admin management (SRS 12.12.1, task 118) ----

    Task AddAsync(Coupon coupon);

    Task UpdateAsync(Coupon coupon);

    /// <summary>Whether a coupon already exists with this code (normalized the same way as <see cref="GetByCodeAsync"/>) - used to reject duplicate codes on create.</summary>
    Task<bool> CodeExistsAsync(string code);

    /// <summary>Filtered, paginated admin coupon list (SRS 12.12.1's "manage coupons" screen).</summary>
    Task<CouponSearchResult> SearchAsync(CouponSearchFilter filter);
}

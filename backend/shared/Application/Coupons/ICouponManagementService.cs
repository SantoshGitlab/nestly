using Nestly.Application.Serviceability;
using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.Coupons;

/// <summary>
/// Admin CRUD over coupons plus redemption reporting (SRS 12.12, task 118).
/// Distinct from <see cref="ICouponService"/>, which is the consumer-facing
/// validate/reserve/redeem flow used at checkout - this is the admin
/// management surface behind it, the same split <c>ICustomerManagementService</c>
/// (admin) draws against the consumer-facing customer services.
/// </summary>
public interface ICouponManagementService
{
    /// <summary>
    /// Active categories, for the coupon form's "applicable category" picker.
    /// Deliberately its own endpoint rather than reusing
    /// <c>ServiceabilityMappingsController</c>'s "categories" lookup - that
    /// one is gated behind "serviceability.read", which Pricing Admin and
    /// Marketing Admin (the two roles that actually hold "coupons.write" per
    /// <c>AdminPermissionCatalog</c>) do not hold, so reusing it would 403 a
    /// role trying to create a coupon that legitimately can.
    /// </summary>
    Task<IReadOnlyList<CategoryLookupResponse>> ListApplicableCategoriesAsync();

    Task<CouponAdminSearchResponse> SearchAsync(CouponAdminSearchRequest request);

    Task<Result<CouponAdminResponse>> GetByIdAsync(Guid id);

    Task<Result<CouponAdminResponse>> CreateAsync(CouponCreateRequest request);

    Task<Result<CouponAdminResponse>> UpdateAsync(Guid id, CouponUpdateRequest request);

    Task<Result> ActivateAsync(Guid id);

    Task<Result> DeactivateAsync(Guid id);

    /// <summary>Redemption reporting (SRS 12.12.2): per-coupon aggregate stats, optionally scoped to one coupon and/or a date range.</summary>
    Task<Result<CouponRedemptionReportResponse>> GetRedemptionReportAsync(CouponRedemptionReportRequest request);
}

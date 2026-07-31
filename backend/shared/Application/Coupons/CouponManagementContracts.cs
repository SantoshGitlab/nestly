using Nestly.Domain;

namespace Nestly.Application.Coupons;

/// <summary>
/// Search/filter criteria for the admin coupon list (SRS 12.12.1). All
/// filters are optional and combine with AND, mirroring
/// <c>CustomerSearchFilter</c>'s shape (SRS 12.4.1).
/// </summary>
public sealed record CouponSearchFilter(
    string? Code,
    bool? IsActive,
    CouponDiscountType? DiscountType,
    CouponCustomerSegment? CustomerSegment,
    Guid? ApplicableCategoryId,
    // When set, only coupons whose validity window covers this instant
    // (SRS 12.12.1 "Active status" as of a point in time).
    DateTime? ValidOnUtc,
    int Page,
    int PageSize);

/// <summary>
/// A page of admin coupon rows plus the total match count, for pagination.
/// Carries the already-enriched <see cref="CouponAdminResponse"/> (category
/// name joined in) rather than the raw <see cref="Coupon"/> entity - the
/// same shape <c>CategoryCityMappingRepository.ListAsync</c> returns for its
/// listing screen, since a read-only list has no reason to round-trip
/// through the domain entity once the display projection is already built.
/// </summary>
public sealed record CouponSearchResult(IReadOnlyList<CouponAdminResponse> Items, int TotalCount);

/// <summary>Query-string shape of an admin coupon search request.</summary>
public sealed record CouponAdminSearchRequest(
    string? Code,
    bool? IsActive,
    CouponDiscountType? DiscountType,
    CouponCustomerSegment? CustomerSegment,
    Guid? ApplicableCategoryId,
    DateTime? ValidOnUtc,
    int Page = 1,
    int PageSize = 20);

/// <summary>Full coupon detail for the admin list/edit screens (SRS 12.12.1's field set).</summary>
public sealed record CouponAdminResponse(
    Guid Id,
    string Code,
    string? Description,
    CouponDiscountType DiscountType,
    decimal DiscountValue,
    decimal? MaxDiscountAmount,
    decimal MinOrderAmount,
    DateTime ValidFromUtc,
    DateTime ValidToUtc,
    bool IsActive,
    int? UsageLimitTotal,
    int? UsageLimitPerCustomer,
    int RedemptionCount,
    Guid? ApplicableCategoryId,
    string? ApplicableCategoryName,
    CouponCustomerSegment CustomerSegment,
    DateTime CreatedAtUtc);

public sealed record CouponAdminSearchResponse(
    IReadOnlyList<CouponAdminResponse> Items,
    int TotalCount,
    int Page,
    int PageSize);

/// <summary>Admin request to create a coupon (SRS 12.12.1). <see cref="Code"/> is set once and never edited afterwards - see <see cref="Coupon.Update"/>.</summary>
public sealed record CouponCreateRequest(
    string Code,
    string? Description,
    CouponDiscountType DiscountType,
    decimal DiscountValue,
    decimal? MaxDiscountAmount,
    decimal MinOrderAmount,
    DateTime ValidFromUtc,
    DateTime ValidToUtc,
    int? UsageLimitTotal,
    int? UsageLimitPerCustomer,
    Guid? ApplicableCategoryId,
    CouponCustomerSegment CustomerSegment);

/// <summary>Admin request to edit every mutable rule dimension of an existing coupon (SRS 12.12.1). Omits <see cref="CouponCreateRequest.Code"/> - see <see cref="Coupon.Update"/>.</summary>
public sealed record CouponUpdateRequest(
    string? Description,
    CouponDiscountType DiscountType,
    decimal DiscountValue,
    decimal? MaxDiscountAmount,
    decimal MinOrderAmount,
    DateTime ValidFromUtc,
    DateTime ValidToUtc,
    int? UsageLimitTotal,
    int? UsageLimitPerCustomer,
    Guid? ApplicableCategoryId,
    CouponCustomerSegment CustomerSegment);

/// <summary>
/// Filters for the redemption report (SRS 12.12.2). Omitting <see cref="CouponId"/>
/// reports across every coupon that has at least one redemption in the
/// window; omitting the date range reports all-time.
/// </summary>
public sealed record CouponRedemptionReportRequest(Guid? CouponId, DateTime? FromUtc, DateTime? ToUtc);

/// <summary>
/// One coupon's redemption stats (SRS 12.12.2: "Redemptions", "Discount
/// total", "Booking count using coupon" - one row per <see cref="CouponRedemption"/>
/// is one booking, so <see cref="RedemptionCount"/> serves both). "Conversion
/// and abuse indicators" is approximated by <see cref="UniqueCustomerCount"/>
/// against <see cref="RedemptionCount"/> - a ratio near 1 means the coupon is
/// spreading across distinct customers as intended; a ratio well above 1
/// (only possible when <see cref="Coupon.UsageLimitPerCustomer"/> allows
/// more than one redemption each) means usage is concentrated in a few
/// accounts, the closest abuse signal the data actually captures without
/// inventing a metric (e.g. impression/view tracking) this system does not
/// record anywhere.
/// </summary>
public sealed record CouponRedemptionReportRow(
    Guid CouponId,
    string Code,
    bool IsActive,
    int RedemptionCount,
    decimal TotalDiscountAmount,
    int UniqueCustomerCount);

public sealed record CouponRedemptionReportResponse(
    IReadOnlyList<CouponRedemptionReportRow> Rows,
    int TotalRedemptions,
    decimal TotalDiscountAmount,
    DateTime? FromUtc,
    DateTime? ToUtc);

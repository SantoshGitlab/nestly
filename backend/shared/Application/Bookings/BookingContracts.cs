using Nestly.Application.Catalog;
using Nestly.Application.Coupons;
using Nestly.Application.Pricing;
using Nestly.Application.Subscriptions;
using Nestly.Domain;

namespace Nestly.Application.Bookings;

/// <summary>
/// A preview of what booking would produce (SRS 11.7, task 57) - the cart
/// model is single-service for now (SRS 11.7.1), so this always describes
/// exactly one service. LocalityId is separate from AddressId rather than
/// derived from it: CustomerAddress.LocalityId (task 154) resolves to null
/// until the address form gains a locality field, so callers must still
/// supply the locality they picked for serviceability/slot checks, the same
/// way the existing frontend location picker already works.
///
/// <paramref name="CouponCode"/> is optional and doubles as the coupon
/// apply/remove mechanism (task 73): supplying a code (re-)applies and
/// recomputes the discount server-side; omitting it (or calling again with
/// null) recomputes with no discount - "remove" is simply "preview again
/// without a code," not a separate stateful operation, since nothing about
/// checkout is server-side session state until the booking itself is created.
/// </summary>
/// <param name="IdempotencyKey">
/// Task 241: only meaningful on POST /bookings (ignored by the POST
/// /bookings/summary preview, which never persists anything) - minted once
/// per checkout attempt on the summary screen and replayed unchanged on any
/// client-side retry of that same attempt (timeout, double-tap, a second open
/// tab). <see cref="IBookingService.CreateAsync"/> returns the
/// already-created booking instead of a new one when it recognises the key,
/// same pattern <see cref="Nestly.Application.Payments.CreatePaymentOrderRequest"/>
/// already established for POST /payments/orders. Optional - a caller that
/// omits it (e.g. RecurringBookingSchedulerService) simply gets no dedup
/// protection, same as an ordinary create today.
/// </param>
public record BookingSummaryRequest(
    Guid ServiceId,
    Guid CityId,
    Guid AddressId,
    Guid LocalityId,
    Guid SlotWindowId,
    DateOnly SlotDate,
    int Quantity,
    IReadOnlyList<AddOnSelection> AddOns,
    string? CouponCode = null,
    string? IdempotencyKey = null);

public record BookingServiceSummary(Guid Id, string Name, string Slug);

public record BookingAddressSummary(
    Guid Id,
    string Label,
    string Line1,
    string? Line2,
    string? Landmark,
    string Pincode,
    string City,
    string State,
    decimal Latitude,
    decimal Longitude,
    string ContactName,
    string ContactMobile);

public record BookingSlotSummary(Guid SlotWindowId, string Name, DateOnly Date, TimeSpan StartTime, TimeSpan EndTime);

/// <summary>
/// Booking summary data (SRS 11.7.2). Wallet credit is omitted - that module
/// doesn't apply at checkout yet (Phase 4's wallet tasks cover the ledger
/// and balance API only, not spending at checkout). <paramref name="Coupon"/>
/// is null when no coupon was applied (or removed); when present,
/// <paramref name="FinalPayable"/> is <c>Price.TotalPayable - Coupon.DiscountAmount</c>
/// (SRS 14.1's formula subtracts the discount after tax/fees, not before) -
/// <see cref="PriceBreakdownResponse"/> itself is always the pre-discount
/// breakdown, so a caller can still show "was / now" pricing.
///
/// <paramref name="SubscriptionBenefit"/> (task 179) is populated
/// automatically from the customer's active subscription, if any - unlike
/// <paramref name="Coupon"/> it needs no code from the caller. A coupon
/// always takes precedence: <paramref name="SubscriptionBenefit"/> is only
/// ever populated when <paramref name="Coupon"/> is null, so the two never
/// stack on the same booking.
/// </summary>
public record BookingSummaryResponse(
    BookingServiceSummary Service,
    IReadOnlyList<ServiceAddOnSummaryResponse> AddOns,
    BookingAddressSummary Address,
    BookingSlotSummary Slot,
    PriceBreakdownResponse Price,
    string? CancellationPolicy,
    string? ReschedulePolicy,
    CouponSummaryResponse? Coupon,
    decimal FinalPayable,
    SubscriptionBenefitSummary? SubscriptionBenefit = null);

/// <summary>
/// One entry in a booking's status timeline (SRS 11.13, task 60c), mirroring
/// <see cref="Nestly.Domain.BookingStatusHistory"/> with the customer-visible
/// label attached so the frontend never has to know the mapping itself.
/// </summary>
public record BookingStatusTimelineEntry(BookingStatus? FromStatus, BookingStatus ToStatus, string ToStatusLabel, string? Reason, DateTime ChangedAtUtc);

/// <summary>
/// Who is coming (task 275). Identity only - the three things a customer
/// needs to recognise the person at the door - and deliberately not the
/// provider's id, phone, email or status: this rides on a general booking
/// read, and a general read is the wrong place to widen PII exposure.
///
/// <paramref name="PhotoUrl"/> and <paramref name="Rating"/> are nullable
/// because no column backs either today (see <see cref="From"/>); the fields
/// exist now so the shape does not change under the frontends when the data
/// arrives, and a null renders as the placeholder avatar / hidden rating a
/// live tracking screen already needs for a brand-new provider.
/// </summary>
public record BookingProviderSummary(string DisplayName, string? PhotoUrl, double? Rating)
{
    /// <summary>
    /// The single mapping from the aggregate, shared by the booking detail
    /// and the live tracking response so the two can never disagree about
    /// who the provider is.
    ///
    /// PhotoUrl and Rating are hardcoded null, in one place, on purpose:
    /// <see cref="Domain.Provider"/> has no photo column, and
    /// <see cref="Domain.Review"/> is scoped to a service, not a provider
    /// (no ProviderId on it, no per-provider aggregate on IReviewRepository),
    /// so there is no honest value to return. Deriving a "provider rating"
    /// from the service reviews left on their bookings would put a number a
    /// customer reads as being about this person on top of data that is not -
    /// a product decision and a schema change, not something to infer inside
    /// a response mapper. Left null rather than faked, and left visible here
    /// rather than dropped from the contract.
    /// </summary>
    public static BookingProviderSummary From(Provider provider) =>
        new(provider.DisplayName, null, null);
}

/// <summary>
/// Full booking detail (SRS 11.13, task 60c). <paramref name="CouponCode"/>/
/// <paramref name="CouponDiscountAmount"/> mirror the booking's own
/// snapshot (immutable once created, unlike the live re-validated preview
/// on <see cref="BookingSummaryResponse"/>). <paramref name="FinalPayable"/>
/// and <c>Price.TotalPayable</c> are equal here and both already
/// coupon-adjusted - unlike the pre-booking preview, a persisted booking has
/// only one "amount actually charged" (<see cref="Domain.Booking.TotalPayableSnapshot"/>),
/// not a separate pre-/post-discount pair. <paramref name="ProviderAssignmentStatus"/>
/// is null until a provider is assigned, then tracks the live
/// <see cref="Domain.BookingProviderAssignment"/> row (task 208) - <see cref="Domain.BookingStatus"/>
/// itself stays "Assigned" through both the offer and the provider's accept,
/// so this is how the customer sees an accept the moment it happens rather
/// than only once the provider starts the job. <paramref name="Provider"/>
/// (task 275) answers the question the status alone cannot - WHO is coming -
/// and is populated off the same live assignment, so it appears and
/// disappears with <paramref name="ProviderAssignmentStatus"/>. Appended
/// last: this is a positional record, and inserting a parameter mid-list
/// would silently re-bind every argument after it at the one call site that
/// builds it.
/// </summary>
public record BookingDetailResponse(
    Guid Id,
    BookingServiceSummary Service,
    IReadOnlyList<ServiceAddOnSummaryResponse> AddOns,
    BookingAddressSummary Address,
    BookingSlotSummary Slot,
    PriceBreakdownResponse Price,
    BookingStatus Status,
    string StatusLabel,
    IReadOnlyList<BookingStatusTimelineEntry> Timeline,
    DateTime CreatedAtUtc,
    string? CouponCode,
    decimal? CouponDiscountAmount,
    decimal FinalPayable,
    BookingProviderAssignmentStatus? ProviderAssignmentStatus,
    BookingProviderSummary? Provider);

/// <summary>A row in the booking list (SRS 11.13, task 60b) - a lighter shape than the detail, for a list screen.</summary>
public record BookingListItemResponse(
    Guid Id,
    string ServiceName,
    DateOnly SlotDate,
    decimal TotalPayable,
    BookingStatus Status,
    string StatusLabel,
    DateTime CreatedAtUtc);

using Nestly.Application.Catalog;
using Nestly.Application.Pricing;
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
/// </summary>
public record BookingSummaryRequest(
    Guid ServiceId,
    Guid CityId,
    Guid AddressId,
    Guid LocalityId,
    Guid SlotWindowId,
    DateOnly SlotDate,
    int Quantity,
    IReadOnlyList<AddOnSelection> AddOns);

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

/// <summary>Booking summary data (SRS 11.7.2). Coupon discount and wallet credit are omitted - neither module exists yet (Phase 4).</summary>
public record BookingSummaryResponse(
    BookingServiceSummary Service,
    IReadOnlyList<ServiceAddOnSummaryResponse> AddOns,
    BookingAddressSummary Address,
    BookingSlotSummary Slot,
    PriceBreakdownResponse Price,
    string? CancellationPolicy,
    string? ReschedulePolicy);

/// <summary>
/// One entry in a booking's status timeline (SRS 11.13, task 60c), mirroring
/// <see cref="Nestly.Domain.BookingStatusHistory"/> with the customer-visible
/// label attached so the frontend never has to know the mapping itself.
/// </summary>
public record BookingStatusTimelineEntry(BookingStatus? FromStatus, BookingStatus ToStatus, string ToStatusLabel, string? Reason, DateTime ChangedAtUtc);

/// <summary>Full booking detail (SRS 11.13, task 60c).</summary>
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
    DateTime CreatedAtUtc);

/// <summary>A row in the booking list (SRS 11.13, task 60b) - a lighter shape than the detail, for a list screen.</summary>
public record BookingListItemResponse(
    Guid Id,
    string ServiceName,
    DateOnly SlotDate,
    decimal TotalPayable,
    BookingStatus Status,
    string StatusLabel,
    DateTime CreatedAtUtc);

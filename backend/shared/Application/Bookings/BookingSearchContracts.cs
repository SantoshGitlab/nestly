using Nestly.Domain;

namespace Nestly.Application.Bookings;

/// <summary>
/// Admin booking-list filter (SRS 12.11.1, task 115a). Every field is
/// optional; an absent field imposes no constraint. There is deliberately no
/// "professional/provider" filter - no such concept exists anywhere in the
/// domain model yet (no assignment entity backs <see cref="BookingStatus.Assigned"/>),
/// so it is omitted rather than invented. "Source" (also listed in SRS
/// 12.11.1) is omitted for the same reason - <see cref="Booking"/> has no
/// channel/source column.
/// </summary>
public sealed record BookingSearchFilter(
    Guid? BookingId,
    string? CustomerName,
    string? CustomerMobile,
    BookingStatus? Status,
    string? City,
    DateOnly? SlotDateFrom,
    DateOnly? SlotDateTo,
    DateTime? CreatedFromUtc,
    DateTime? CreatedToUtc,
    Guid? ServiceId,
    Guid? CategoryId,
    string? CouponCode,
    int Page,
    int PageSize);

/// <summary>A page of admin booking search results, with the loaded aggregates and the total match count.</summary>
public sealed record BookingSearchResult(IReadOnlyList<Booking> Rows, int TotalCount);

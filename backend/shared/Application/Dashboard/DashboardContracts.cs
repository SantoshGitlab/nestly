namespace Nestly.Application.Dashboard;

/// <summary>
/// Dashboard KPI filters (SRS 12.3.2). Every field is optional:
/// <see cref="Nestly.Infrastructure.Services.DashboardQueryService"/> defaults
/// an unset <paramref name="DateFrom"/>/<paramref name="DateTo"/> to "today" -
/// the natural default given SRS 12.3.1's baseline metric is "Bookings today" -
/// and leaving <paramref name="City"/>/<paramref name="Category"/> unset
/// applies no restriction on that dimension.
/// </summary>
/// <param name="City">
/// Matched case-insensitively against <see cref="Nestly.Domain.Booking.AddressCitySnapshot"/> -
/// the booking's own address snapshot, not a live city lookup, since a
/// booking's address never changes after the fact (SRS 14.1).
/// </param>
/// <param name="Category">
/// The slug of the <see cref="Nestly.Domain.Category"/> that owns the booked
/// service - the same identifier the public catalog API already keys
/// category lookups by (<c>CategoriesController.GetDetail</c>).
/// </param>
public sealed record DashboardFilterRequest(DateOnly? DateFrom, DateOnly? DateTo, string? City, string? Category);

/// <summary>
/// SRS 12.3.1's KPI widget set for the resolved filter window: bookings,
/// revenue, cancellations, refunds, and open support tickets. Echoes back the
/// resolved <paramref name="DateFrom"/>/<paramref name="DateTo"/> so a caller
/// that omitted either can tell which window the numbers actually cover.
/// </summary>
public sealed record DashboardKpiResponse(
    DateOnly DateFrom,
    DateOnly DateTo,
    int BookingsCount,
    decimal RevenueTotal,
    int CancellationsCount,
    decimal RefundAmountTotal,
    int OpenSupportTicketsCount);

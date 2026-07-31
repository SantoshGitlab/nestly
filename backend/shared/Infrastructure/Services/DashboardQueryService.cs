using Microsoft.EntityFrameworkCore;
using Nestly.Application.Dashboard;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// SRS 12.3's dashboard KPI query (task 99). Reads <see cref="NestlyDbContext"/>
/// directly rather than through a per-aggregate repository: this is a single
/// cross-aggregate read model (Booking/PaymentTransaction/BookingCancellation/
/// RefundTransaction/SupportTicket) with no write side of its own, so routing
/// it through five separate repositories would only add indirection without
/// improving testability or reuse (YAGNI).
/// </summary>
public sealed class DashboardQueryService : IDashboardQueryService
{
    private static readonly SupportTicketStatus[] TerminalTicketStatuses =
        [SupportTicketStatus.Resolved, SupportTicketStatus.Closed];

    private readonly NestlyDbContext _context;

    public DashboardQueryService(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task<Result<DashboardKpiResponse>> GetKpisAsync(DashboardFilterRequest filter)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        DateOnly dateFrom = filter.DateFrom ?? today;
        DateOnly dateTo = filter.DateTo ?? dateFrom;

        if (dateFrom > dateTo)
        {
            return Error.Validation("Dashboard.InvalidDateRange", "The 'from' date cannot be after the 'to' date.");
        }

        DateTime rangeStartUtc = dateFrom.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        DateTime rangeEndUtc = dateTo.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        string? city = string.IsNullOrWhiteSpace(filter.City) ? null : filter.City.Trim();
        string? category = string.IsNullOrWhiteSpace(filter.Category) ? null : filter.Category.Trim();

        // Bookings matching the City/Category dimensions, independent of the
        // date range - reused below both for the in-range booking metrics and
        // to scope tickets (which keep their own CreatedAtUtc) to the same
        // city/category, even when the ticket's linked booking predates the
        // window.
        IQueryable<Booking> dimensionFilteredBookings = _context.Bookings;

        if (city is not null)
        {
            dimensionFilteredBookings = dimensionFilteredBookings.Where(b => b.AddressCitySnapshot.ToLower() == city.ToLower());
        }

        if (category is not null)
        {
            IQueryable<Guid> serviceIdsInCategory = _context.Set<Service>()
                .Where(s => _context.Set<Category>().Any(c => c.Id == s.CategoryId && c.Slug.ToLower() == category.ToLower()))
                .Select(s => s.Id);

            dimensionFilteredBookings = dimensionFilteredBookings
                .Where(b => b.Items.Any(item => serviceIdsInCategory.Contains(item.ServiceId)));
        }

        IQueryable<Booking> bookingsInRange = dimensionFilteredBookings
            .Where(b => b.CreatedAtUtc >= rangeStartUtc && b.CreatedAtUtc <= rangeEndUtc);

        IQueryable<Guid> bookingIdsInRange = bookingsInRange.Select(b => b.Id);

        int bookingsCount = await bookingsInRange.CountAsync();

        // Summed client-side, not via SumAsync - same approach RefundService/
        // CancellationService/RescheduleService already use for a decimal
        // sum over refunds: SQLite (this project's unit-test provider) has
        // no server-side translation for a decimal aggregate, and Postgres
        // has no difficulty summing the handful of rows a filtered dashboard
        // window returns, so there's no performance reason to require one.
        List<decimal> successfulPaymentAmounts = await _context.PaymentTransactions
            .Where(p => p.Status == PaymentTransactionStatus.Success && bookingIdsInRange.Contains(p.BookingId))
            .Select(p => p.Amount)
            .ToListAsync();
        decimal revenueTotal = successfulPaymentAmounts.Sum();

        int cancellationsCount = await _context.BookingCancellations
            .Where(c => bookingIdsInRange.Contains(c.BookingId))
            .CountAsync();

        List<decimal> refundedAmounts = await _context.RefundTransactions
            .Where(r => r.Status == RefundStatus.Refunded && bookingIdsInRange.Contains(r.BookingId))
            .Select(r => r.Amount)
            .ToListAsync();
        decimal refundAmountTotal = refundedAmounts.Sum();

        IQueryable<SupportTicket> ticketsInRange = _context.SupportTickets
            .Where(t => t.CreatedAtUtc >= rangeStartUtc && t.CreatedAtUtc <= rangeEndUtc);

        if (city is not null || category is not null)
        {
            IQueryable<Guid> dimensionFilteredBookingIds = dimensionFilteredBookings.Select(b => b.Id);
            ticketsInRange = ticketsInRange.Where(t => t.BookingId != null && dimensionFilteredBookingIds.Contains(t.BookingId!.Value));
        }

        int openSupportTicketsCount = await ticketsInRange
            .Where(t => !TerminalTicketStatuses.Contains(t.Status))
            .CountAsync();

        return new DashboardKpiResponse(dateFrom, dateTo, bookingsCount, revenueTotal, cancellationsCount, refundAmountTotal, openSupportTicketsCount);
    }
}

using Microsoft.EntityFrameworkCore;
using Nestly.Application;
using Nestly.Application.RecurringBookings;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// <inheritdoc cref="IRecurringBookingPlanAdminService"/>
///
/// Reads <see cref="NestlyDbContext"/> directly rather than through
/// <c>IRecurringBookingPlanRepository</c>, for the reason
/// <c>ReportingQueryService</c> and <c>DashboardQueryService</c> already
/// document: these are read-only cross-aggregate queries with no write side,
/// and the plan repository's methods all load a plan with its add-ons for
/// mutation, which is the opposite of what a count needs.
///
/// Every aggregate below is a <c>GroupBy</c>/<c>CountAsync</c> that runs in
/// the database. None of them materializes plan or booking rows - a platform
/// with 50,000 standing plans must not stream 50,000 rows into the API
/// process to answer "how many are paused". The only work done in memory is
/// zero-filling status/frequency buckets the database had no rows for and
/// adding up the per-day counts, both of which operate on a handful of
/// already-aggregated rows rather than on the underlying tables.
/// </summary>
public sealed class RecurringBookingPlanAdminService : IRecurringBookingPlanAdminService
{
    /// <summary>
    /// Statuses that mean the visit is off, excluded from
    /// <c>UpcomingOccurrenceVolume</c>: counting them would tell an admin to
    /// staff for work nobody is going to do. Deliberately not
    /// <c>BookingLifecycle.IsTerminal</c> - <see cref="BookingStatus.Completed"/>
    /// is terminal too, and a completed booking inside the horizon is work
    /// that did happen, so it still belongs in the volume.
    /// </summary>
    private static readonly BookingStatus[] CalledOffStatuses =
        [BookingStatus.CancelledByCustomer, BookingStatus.CancelledByAdmin, BookingStatus.Expired];

    private readonly NestlyDbContext _context;

    public RecurringBookingPlanAdminService(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task<Result<AdminRecurringPlanSearchResponse>> SearchAsync(AdminRecurringPlanSearchRequest request)
    {
        IQueryable<RecurringBookingPlan> plans = _context.RecurringBookingPlans.AsNoTracking();


        if (request.Status is { } status)
        {
            plans = plans.Where(p => p.Status == status);
        }

        if (request.Frequency is { } frequency)
        {
            plans = plans.Where(p => p.Frequency == frequency);
        }

        if (request.CustomerId is { } customerId)
        {
            plans = plans.Where(p => p.CustomerId == customerId);
        }

        if (request.ServiceId is { } serviceId)
        {
            plans = plans.Where(p => p.ServiceId == serviceId);
        }

        int totalCount = await plans.CountAsync();

        // Inner joins are safe here: RecurringBookingPlanConfiguration puts a
        // Restrict foreign key on both CustomerId and ServiceId, so neither
        // referenced row can be deleted out from under a plan.
        var rows =
            from plan in plans
            join customer in _context.Set<Customer>().AsNoTracking() on plan.CustomerId equals customer.Id
            join service in _context.Set<Service>().AsNoTracking() on plan.ServiceId equals service.Id
            select new { Plan = plan, CustomerName = customer.Name, ServiceName = service.Name };

        (int page, int pageSize) = PagedQueryExtensions.Normalize(request.Page, request.PageSize);

        // Ordered and paged on the joined columns, then projected - ordering by
        // a member of the final record instead does not translate, because the
        // projection is a constructor call the provider cannot see through.
        // Id breaks ties on CreatedAtUtc so two plans created in the same tick
        // cannot swap places between page 1 and page 2 (PagedQueryExtensions:
        // an unstably ordered paged query has no stable page boundaries).
        var items = await rows
            .OrderByDescending(r => r.Plan.CreatedAtUtc)
            .ThenBy(r => r.Plan.Id)
            .ApplyPaging(page, pageSize)
            .Select(r => new AdminRecurringPlanSummaryResponse(
                r.Plan.Id,
                r.Plan.CustomerId,
                r.CustomerName,
                r.Plan.ServiceId,
                r.ServiceName,
                r.Plan.Frequency,
                r.Plan.RecurrenceDayOfWeek,
                r.Plan.RecurrenceDayOfMonth,
                r.Plan.StartDate,
                r.Plan.EndDate,
                r.Plan.OccurrenceCount,
                r.Plan.CompletedOccurrenceCount,
                r.Plan.NextOccurrenceDate,
                r.Plan.Status,
                r.Plan.CreatedAtUtc))
            .ToListAsync();

        return new AdminRecurringPlanSearchResponse(items, totalCount, page, pageSize);
    }

    public async Task<Result<AdminRecurringPlanReportResponse>> GetReportAsync(AdminRecurringPlanReportRequest request)
    {
        var fromDate = request.FromDate ?? DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var toDate = request.ToDate ?? fromDate.AddDays(IRecurringBookingPlanAdminService.DefaultHorizonDays);

        if (toDate < fromDate)
        {
            return Error.Validation("Reports.InvalidDateRange", "The 'to' date cannot be before the 'from' date.");
        }

        var byStatus = await _context.RecurringBookingPlans
            .GroupBy(p => p.Status)
            .Select(g => new RecurringPlanStatusCountRow(g.Key, g.Count()))
            .ToListAsync();

        var activeByFrequency = await _context.RecurringBookingPlans
            .Where(p => p.Status == RecurringBookingPlanStatus.Active)
            .GroupBy(p => p.Frequency)
            .Select(g => new RecurringPlanFrequencyCountRow(g.Key, g.Count()))
            .ToListAsync();

        int plansDueInHorizon = await _context.RecurringBookingPlans
            .CountAsync(p => p.Status == RecurringBookingPlanStatus.Active
                && p.NextOccurrenceDate >= fromDate
                && p.NextOccurrenceDate <= toDate);

        // Task 296's forward link is what makes this answerable without a join
        // to recurring_booking_occurrence: a booking generated by a plan
        // carries the plan's id, so "recurring work in this window" is a
        // predicate on the booking table alone.
        var volumeByDate = await _context.Bookings
            .Where(b => b.RecurringBookingPlanId != null
                && b.SlotDate >= fromDate
                && b.SlotDate <= toDate
                && !CalledOffStatuses.Contains(b.Status))
            .GroupBy(b => b.SlotDate)
            // Ordered by the grouping key, not by the projected record's
            // property: the projection is a constructor call the provider
            // cannot see through, so ordering after it falls back to client
            // evaluation - which EF refuses outright.
            .OrderBy(g => g.Key)
            .Select(g => new RecurringPlanDailyVolumeRow(g.Key, g.Count()))
            .ToListAsync();

        return new AdminRecurringPlanReportResponse(
            byStatus.Sum(r => r.PlanCount),
            ZeroFill(byStatus, Enum.GetValues<RecurringBookingPlanStatus>(), r => r.Status, s => new RecurringPlanStatusCountRow(s, 0)),
            ZeroFill(activeByFrequency, Enum.GetValues<RecurringBookingRecurrenceFrequency>(), r => r.Frequency, f => new RecurringPlanFrequencyCountRow(f, 0)),
            fromDate,
            toDate,
            plansDueInHorizon,
            volumeByDate.Sum(r => r.BookingCount),
            volumeByDate);
    }

    /// <summary>
    /// Adds a zero row for every enum member the grouped query returned
    /// nothing for, in declaration order. A report that silently omits
    /// "Cancelled: 0" reads as "cancellations were not measured" rather than
    /// "there were none", and a chart built off it changes shape the first
    /// time a bucket empties.
    /// </summary>
    private static IReadOnlyList<TRow> ZeroFill<TRow, TKey>(
        IReadOnlyList<TRow> rows,
        TKey[] allKeys,
        Func<TRow, TKey> keyOf,
        Func<TKey, TRow> emptyRow)
        where TKey : struct
    {
        var byKey = rows.ToDictionary(keyOf);
        return allKeys.Select(key => byKey.TryGetValue(key, out var row) ? row : emptyRow(key)).ToList();
    }
}

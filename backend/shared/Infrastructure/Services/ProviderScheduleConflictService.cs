using Microsoft.EntityFrameworkCore;
using Nestly.Application.ProviderManagement;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence;

namespace Nestly.Infrastructure.Services;

/// <summary>See <see cref="IProviderScheduleConflictService"/>.</summary>
public class ProviderScheduleConflictService : IProviderScheduleConflictService
{
    // The question spans BookingProviderAssignment (who is committed) and
    // Booking (when the slot actually is), and no single existing repository
    // owns that join - read directly off the shared context, same as
    // BookingProviderAssignmentService/RefundService do for their own
    // cross-aggregate needs.
    private readonly NestlyDbContext _context;
    private readonly IProviderJobOccupancyService _occupancyService;

    public ProviderScheduleConflictService(NestlyDbContext context, IProviderJobOccupancyService occupancyService)
    {
        _context = context;
        _occupancyService = occupancyService;
    }

    public async Task<ProviderScheduleConflict?> FindConflictAsync(Guid providerId, Booking booking)
    {
        ArgumentNullException.ThrowIfNull(booking);

        // Statuses that still occupy the provider's schedule. Assigned/Accepted
        // are the outstanding ones; Completed is included so a finished job goes
        // on blocking its slot window exactly as it did before the assignment
        // gained its own terminal state (it used to stay Accepted). Refining
        // that window down to the actual finish time for a non-duration service
        // - the early-release step - is a deliberate follow-up, kept separate so
        // this change alters no scheduling behaviour on its own. A
        // Rejected/Reassigned/Withdrawn row is nobody's commitment and never blocks.
        var liveStatuses = new[]
        {
            BookingProviderAssignmentStatus.Assigned,
            BookingProviderAssignmentStatus.Accepted,
            BookingProviderAssignmentStatus.Completed
        };

        // Narrowed in SQL to this one provider's live jobs on this one date -
        // a handful of rows at most - and only then compared as intervals in
        // memory: TimeSpan comparisons don't reliably translate on the SQLite
        // provider this test suite runs against (the same reason
        // ProviderAssignmentEligibilityService matches availability windows in
        // memory, and ProviderAvailabilityWindowRepository orders in memory).
        var sameDayJobs = await _context.Set<BookingProviderAssignment>()
            .Join(_context.Set<Booking>(), a => a.BookingId, b => b.Id, (a, b) => new { Assignment = a, Booking = b })
            .Where(x =>
                x.Assignment.ProviderId == providerId &&
                liveStatuses.Contains(x.Assignment.Status) &&
                x.Booking.SlotDate == booking.SlotDate &&
                // A booking is never its own conflict - and because an
                // assignment row belongs to exactly one booking, this also
                // excludes the row being superseded when the same booking is
                // reassigned.
                x.Booking.Id != booking.Id)
            .Select(x => new
            {
                x.Booking.Id,
                x.Booking.SlotDate,
                StartTime = x.Booking.SlotStartTimeSnapshot,
                EndTime = x.Booking.SlotEndTimeSnapshot,
                x.Assignment.Status,
                x.Assignment.CompletedAt,
                x.Booking.IsDurationBasedSnapshot,
            })
            .ToListAsync();

        // Half-open [start, effective end) overlap - see FindConflictAsync's
        // doc comment. The effective end (IProviderJobOccupancyService) is the
        // slot's own end for anything not yet verified-complete or duration-
        // based, and the job's actual finish time otherwise - the early-
        // release/overrun step. It is used only for the overlap boundary, not
        // in the returned conflict: the booking's own booked slot end is what
        // an admin reading the resulting error message needs, not an inferred
        // completion time. Earliest-first so the booking named in that message
        // is deterministic when more than one job clashes.
        var conflict = sameDayJobs
            .Select(job => new
            {
                job.Id,
                job.SlotDate,
                job.StartTime,
                job.EndTime,
                EffectiveEndTime = _occupancyService.EffectiveEndTime(new JobOccupancy(
                    job.Status, job.CompletedAt, job.IsDurationBasedSnapshot, job.SlotDate, job.StartTime, job.EndTime)),
            })
            .Where(job => booking.SlotStartTimeSnapshot < job.EffectiveEndTime && job.StartTime < booking.SlotEndTimeSnapshot)
            .OrderBy(job => job.StartTime)
            .FirstOrDefault();

        return conflict is null
            ? null
            : new ProviderScheduleConflict(conflict.Id, conflict.SlotDate, conflict.StartTime, conflict.EndTime);
    }
}

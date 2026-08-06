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

    public ProviderScheduleConflictService(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task<ProviderScheduleConflict?> FindConflictAsync(Guid providerId, Booking booking)
    {
        ArgumentNullException.ThrowIfNull(booking);

        // "Live" mirrors BookingProviderAssignmentStatus's own doc comment
        // (Assigned/Accepted are the only statuses ever "outstanding" at
        // once) - a Rejected/Reassigned/Withdrawn row is nobody's commitment
        // any more and must not block a new assignment.
        var liveStatuses = new[] { BookingProviderAssignmentStatus.Assigned, BookingProviderAssignmentStatus.Accepted };

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
            })
            .ToListAsync();

        // Half-open [start, end) overlap - see FindConflictAsync's doc
        // comment. Earliest-first so the booking named in the admin's error
        // message is deterministic when more than one job clashes.
        var conflict = sameDayJobs
            .Where(job => booking.SlotStartTimeSnapshot < job.EndTime && job.StartTime < booking.SlotEndTimeSnapshot)
            .OrderBy(job => job.StartTime)
            .FirstOrDefault();

        return conflict is null
            ? null
            : new ProviderScheduleConflict(conflict.Id, conflict.SlotDate, conflict.StartTime, conflict.EndTime);
    }
}

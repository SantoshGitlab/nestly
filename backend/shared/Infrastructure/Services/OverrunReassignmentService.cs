using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nestly.Application;
using Nestly.Application.Bookings;
using Nestly.Application.ProviderManagement;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence;

namespace Nestly.Infrastructure.Services;

/// <summary>See <see cref="IOverrunReassignmentService"/>.</summary>
public class OverrunReassignmentService : IOverrunReassignmentService
{
    // Same cross-aggregate join as ProviderScheduleConflictService/
    // ProviderTravelFeasibilityService, to find the candidate queued bookings;
    // each candidate is then re-checked through the same feasibility service
    // the assignment engine already trusts, rather than a second copy of that
    // logic here.
    private readonly NestlyDbContext _context;
    private readonly IBookingRepository _bookingRepository;
    private readonly IBookingProviderAssignmentRepository _assignmentRepository;
    private readonly IProviderTravelFeasibilityService _travelFeasibilityService;
    private readonly ILogger<OverrunReassignmentService> _logger;

    public OverrunReassignmentService(
        NestlyDbContext context,
        IBookingRepository bookingRepository,
        IBookingProviderAssignmentRepository assignmentRepository,
        IProviderTravelFeasibilityService travelFeasibilityService,
        ILogger<OverrunReassignmentService> logger)
    {
        _context = context;
        _bookingRepository = bookingRepository;
        _assignmentRepository = assignmentRepository;
        _travelFeasibilityService = travelFeasibilityService;
        _logger = logger;
    }

    public async Task ReassignInfeasibleQueuedJobsAsync(
        Guid providerId, DateOnly slotDate, Guid completedBookingId, CancellationToken cancellationToken = default)
    {
        // Queued: accepted by this provider, not yet started - the booking is
        // still sitting at Assigned. A job already active (EnRoute/Arrived/
        // InProgress) is not re-checked here: IProviderActiveJobLimitService's
        // one-active-job rule already guarantees there is at most one, and it
        // is not "queued" in the sense this method cares about.
        var queuedBookingIds = await _context.Set<BookingProviderAssignment>()
            .Join(_context.Set<Booking>(), a => a.BookingId, b => b.Id, (a, b) => new { Assignment = a, Booking = b })
            .Where(x =>
                x.Assignment.ProviderId == providerId &&
                x.Assignment.Status == BookingProviderAssignmentStatus.Accepted &&
                x.Booking.Status == BookingStatus.Assigned &&
                x.Booking.SlotDate == slotDate &&
                x.Booking.Id != completedBookingId)
            .Select(x => x.Booking.Id)
            .ToListAsync(cancellationToken);

        foreach (var queuedBookingId in queuedBookingIds)
        {
            var queuedBooking = await _bookingRepository.GetByIdAsync(queuedBookingId);
            if (queuedBooking is null)
            {
                continue;
            }

            var conflict = await _travelFeasibilityService.FindConflictAsync(providerId, queuedBooking, cancellationToken);
            if (conflict is null)
            {
                continue;
            }

            var queuedAssignment = await _assignmentRepository.GetActiveByBookingAsync(queuedBookingId);
            if (queuedAssignment is null || queuedAssignment.ProviderId != providerId)
            {
                // Already moved on for an unrelated reason between the lookup
                // above and here (a race, however unlikely) - nothing left to
                // withdraw.
                continue;
            }

            _logger.LogWarning(
                "Provider {ProviderId}'s prior job overran; queued booking {QueuedBookingId} is no longer travel-feasible ({RequiredSeconds}s needed, {GapSeconds}s available) and is being returned for reassignment.",
                providerId, queuedBookingId, conflict.TravelSeconds + conflict.BufferSeconds, conflict.GapSeconds);

            queuedAssignment.Withdraw();
            await _assignmentRepository.UpdateAsync(queuedAssignment);

            queuedBooking.AssignProvider(null);
            queuedBooking.TransitionTo(
                BookingStatus.AwaitingFulfilment,
                "The assigned provider's prior job overran; this assignment is no longer feasible and needs reassignment.");
            await _bookingRepository.UpdateAsync(queuedBooking);
        }
    }
}

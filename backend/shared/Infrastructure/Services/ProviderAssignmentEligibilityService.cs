using Microsoft.EntityFrameworkCore;
using Nestly.Application;
using Nestly.Application.Bookings;
using Nestly.Application.ProviderManagement;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence;

namespace Nestly.Infrastructure.Services;

/// <summary>See <see cref="IProviderAssignmentEligibilityService"/>.</summary>
public class ProviderAssignmentEligibilityService : IProviderAssignmentEligibilityService
{
    private readonly IBookingRepository _bookingRepository;
    private readonly IProviderAvailabilityWindowRepository _availabilityWindowRepository;
    private readonly IProviderBlackoutDateRepository _blackoutDateRepository;
    private readonly IProviderCapacityRepository _capacityRepository;
    private readonly IProviderScheduleConflictService _scheduleConflictService;
    private readonly IProviderTravelFeasibilityService _travelFeasibilityService;
    private readonly NestlyDbContext _context;

    public ProviderAssignmentEligibilityService(
        IBookingRepository bookingRepository,
        IProviderAvailabilityWindowRepository availabilityWindowRepository,
        IProviderBlackoutDateRepository blackoutDateRepository,
        IProviderCapacityRepository capacityRepository,
        IProviderScheduleConflictService scheduleConflictService,
        IProviderTravelFeasibilityService travelFeasibilityService,
        NestlyDbContext context)
    {
        _bookingRepository = bookingRepository;
        _availabilityWindowRepository = availabilityWindowRepository;
        _blackoutDateRepository = blackoutDateRepository;
        _capacityRepository = capacityRepository;
        _scheduleConflictService = scheduleConflictService;
        _travelFeasibilityService = travelFeasibilityService;
        _context = context;
    }

    public async Task<bool> IsEligibleAsync(Guid providerId, Guid bookingId, CancellationToken cancellationToken = default)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId);
        if (booking is null)
        {
            return false;
        }

        // Task 288: unconditional, and deliberately ahead of - and
        // independent of - the optional ProviderCapacity limits below. A
        // provider already committed to an overlapping job cannot take this
        // one no matter what their configured limits say, which matters
        // because today no provider has a ProviderCapacity row at all (see
        // HasCapacityAsync) and so every one of them would otherwise be
        // treated as having unlimited simultaneous capacity.
        if (await _scheduleConflictService.FindConflictAsync(providerId, booking) is not null)
        {
            return false;
        }

        if (!await IsAvailableAsync(providerId, booking))
        {
            return false;
        }

        if (!await HasCapacityAsync(providerId, booking))
        {
            return false;
        }

        // Task 289: last, and only for a candidate nothing else has already
        // refused. Every check above is a local database read; this one can
        // cost a billed route lookup, so a candidate rejected for a blackout
        // date or a full day must never spend one.
        return await _travelFeasibilityService.FindConflictAsync(providerId, booking, cancellationToken) is null;
    }

    private async Task<bool> IsAvailableAsync(Guid providerId, Booking booking)
    {
        var blackouts = await _blackoutDateRepository.GetByProviderAsync(providerId);
        if (blackouts.Any(b => b.CoversDate(booking.SlotDate)))
        {
            return false;
        }

        // Filtered/matched in memory, not translated to SQL - TimeSpan
        // comparisons don't reliably translate on the SQLite provider this
        // test suite runs against (see ProviderAvailabilityWindowRepository's
        // own doc comment for the identical ORDER BY case), and a provider's
        // weekly schedule is a handful of rows at most, never worth a
        // database-side filter.
        var windows = await _availabilityWindowRepository.GetByProviderAsync(providerId);
        return windows.Any(w =>
            w.IsActive &&
            w.DayOfWeek == booking.SlotDate.DayOfWeek &&
            w.StartTime <= booking.SlotStartTimeSnapshot &&
            w.EndTime >= booking.SlotEndTimeSnapshot);
    }

    private async Task<bool> HasCapacityAsync(Guid providerId, Booking booking)
    {
        var capacity = await _capacityRepository.GetByProviderAsync(providerId);
        if (capacity is null)
        {
            // No limits configured (and today, nothing in this codebase can
            // configure them - see IProviderCapacityRepository's doc
            // comment) - unlimited *concurrent-job-count* policy. Not
            // unlimited double-booking: IsEligibleAsync has already refused
            // any overlapping slot through IProviderScheduleConflictService
            // (task 288), which is exactly the invariant this branch used to
            // leak.
            return true;
        }

        // Statuses that count against the provider's configured load. Completed
        // is included so a finished job keeps counting exactly as it did while
        // it stayed Accepted - releasing that count once the job is genuinely
        // done is part of the early-release follow-up, kept separate so this
        // change is behaviour-neutral. Rejected/Reassigned/Withdrawn never count.
        var liveStatuses = new[]
        {
            BookingProviderAssignmentStatus.Assigned,
            BookingProviderAssignmentStatus.Accepted,
            BookingProviderAssignmentStatus.Completed
        };

        if (capacity.MaxJobsPerDay is { } maxPerDay)
        {
            int countForDay = await _context.Set<BookingProviderAssignment>()
                .Join(_context.Set<Booking>(), a => a.BookingId, b => b.Id, (a, b) => new { Assignment = a, Booking = b })
                .CountAsync(x =>
                    x.Assignment.ProviderId == providerId &&
                    liveStatuses.Contains(x.Assignment.Status) &&
                    x.Booking.SlotDate == booking.SlotDate);

            if (countForDay >= maxPerDay)
            {
                return false;
            }
        }

        if (capacity.MaxJobsPerSlot is { } maxPerSlot)
        {
            // Slot-window identity, not clock time: two different windows
            // that overlap in time (09:00-11:00 vs 10:00-12:00) do not
            // collide here and never did. That is fine as a *policy* counter
            // ("how many jobs in one named window"), but it is not what stops
            // a provider being double-booked - the unconditional overlap
            // check in IsEligibleAsync is (task 288).
            int countForSlot = await _context.Set<BookingProviderAssignment>()
                .Join(_context.Set<Booking>(), a => a.BookingId, b => b.Id, (a, b) => new { Assignment = a, Booking = b })
                .CountAsync(x =>
                    x.Assignment.ProviderId == providerId &&
                    liveStatuses.Contains(x.Assignment.Status) &&
                    x.Booking.SlotDate == booking.SlotDate &&
                    x.Booking.SlotWindowId == booking.SlotWindowId);

            if (countForSlot >= maxPerSlot)
            {
                return false;
            }
        }

        return true;
    }
}

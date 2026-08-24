using Nestly.Application.Abstractions.Time;
using Nestly.Application.ProviderManagement;
using Nestly.Domain;

namespace Nestly.Infrastructure.Services;

/// <summary>See <see cref="IProviderJobOccupancyService"/>.</summary>
public class ProviderJobOccupancyService : IProviderJobOccupancyService
{
    private readonly IBusinessClock _clock;

    public ProviderJobOccupancyService(IBusinessClock clock)
    {
        _clock = clock;
    }

    public TimeSpan EffectiveEndTime(JobOccupancy job)
    {
        // The slot's own end is the answer for anything not yet verified-complete,
        // and - unconditionally - for a duration-based service (see the interface
        // doc comment: the booked window is a fixed customer promise, never
        // inferred from a timestamp in either direction).
        if (job.AssignmentStatus != BookingProviderAssignmentStatus.Completed
            || job.IsDurationBasedSnapshot
            || job.CompletedAtUtc is not DateTime completedAtUtc)
        {
            return job.SlotEndTimeSnapshot;
        }

        var completedLocal = _clock.ToBusinessLocal(completedAtUtc);
        var completedDate = DateOnly.FromDateTime(completedLocal);

        if (completedDate > job.SlotDate)
        {
            // Overran past midnight - a same-day TimeSpan cannot express "the
            // next day", so occupy the rest of the slot date rather than
            // under-count how long the provider was genuinely still on this
            // job. Correct-but-coarse: a same-day comparison further out than
            // this is out of scope for a v1 implementation.
            return TimeSpan.FromHours(24);
        }

        if (completedDate < job.SlotDate)
        {
            // Should not happen under normal operation (completion cannot
            // precede the slot date), but a clock/timezone anomaly must never
            // silently *shrink* occupancy below the booked slot.
            return job.SlotEndTimeSnapshot;
        }

        // Never earlier than the slot's own start: a job cannot have
        // "finished" before it began, and treating that as an early release
        // would silently disable the overlap/travel checks for this job
        // entirely - a data anomaly should never widen the provider's
        // apparent availability.
        var completedTimeOfDay = completedLocal.TimeOfDay;
        return completedTimeOfDay < job.SlotStartTimeSnapshot ? job.SlotStartTimeSnapshot : completedTimeOfDay;
    }
}

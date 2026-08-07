namespace Nestly.Application.ProviderManagement;

/// <summary>
/// Task 245: the per-candidate gate the automatic-assignment engine (task
/// 246) applies to each of task 244's ranked candidates in turn, picking the
/// first one that passes. Deliberately separate from
/// <see cref="IProviderMatchingService"/> - matching answers "could this
/// provider ever take this kind of job here," this answers "can they take
/// THIS ONE, right now" (blackout dates, weekly hours, today's load).
/// </summary>
public interface IProviderAssignmentEligibilityService
{
    /// <summary>
    /// True if the provider has no other live job overlapping the booking's
    /// slot (task 288 - see <see cref="IProviderScheduleConflictService"/>;
    /// checked first and unconditionally, since it is a physical invariant
    /// rather than a configurable limit), their weekly availability covers
    /// the booking's slot (day-of-week + full time-range containment), the
    /// day isn't inside one of their blackout ranges, and taking this booking
    /// would not exceed <c>ProviderCapacity.MaxJobsPerDay</c>/<c>MaxJobsPerSlot</c>
    /// (hard limits here, unlike manual admin assignment which still treats
    /// them as advisory - PROVIDER.md OPEN DECISIONS - AUTOMATIC ASSIGNMENT
    /// #2; the overlap check above is the one rule both paths share). A
    /// provider with no availability windows configured at all is never
    /// eligible - "no schedule on file" is not "always available."
    ///
    /// Finally - and only once every free check above has passed, since this
    /// one can cost a billed route lookup - the day has to be drivable: task
    /// 289 (<see cref="IProviderTravelFeasibilityService"/>) refuses a
    /// candidate whose adjacent same-day jobs are further away by road than the
    /// gaps around this booking allow. That check is the one part of this gate
    /// with a kill switch of its own
    /// (<c>AutoAssignmentOptions.TravelBufferEnabled</c>), because unlike the
    /// rest it depends on a third-party estimate.
    /// </summary>
    Task<bool> IsEligibleAsync(Guid providerId, Guid bookingId, CancellationToken cancellationToken = default);
}

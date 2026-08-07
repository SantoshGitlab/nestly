using Nestly.Application.Routing;
using Nestly.Domain;

namespace Nestly.Application.ProviderManagement;

/// <summary>Which of the two neighbouring jobs a provider cannot drive between in the time available.</summary>
public enum ProviderTravelDirection
{
    /// <summary>The leg from the previous same-day job's address to this booking's.</summary>
    FromPreviousBooking = 0,

    /// <summary>The leg from this booking's address to the following same-day job's.</summary>
    ToFollowingBooking = 1
}

/// <summary>
/// Task 289: the neighbouring job the provider cannot get to or from in time,
/// and the arithmetic that says so - so a rejection is explainable rather than
/// a silent skip.
/// </summary>
/// <param name="BookingId">The adjacent same-day booking on the other end of the drive.</param>
/// <param name="Direction">Which side of this booking that job sits on.</param>
/// <param name="GapSeconds">Idle time between the two slots.</param>
/// <param name="TravelSeconds">Estimated road travel time for the leg.</param>
/// <param name="BufferSeconds">The fixed parking/handover allowance added on top.</param>
/// <param name="Source">Whether the travel time was measured or approximated - a sandbox figure still blocks the assignment, but is worth knowing about.</param>
public sealed record ProviderTravelConflict(
    Guid BookingId,
    ProviderTravelDirection Direction,
    int GapSeconds,
    int TravelSeconds,
    int BufferSeconds,
    RouteEstimateSource Source);

/// <summary>
/// Task 289: "a provider finishing at 11:00 across the city cannot start an
/// 11:00 job." Task 288 stopped literal overlap; this stops the next physical
/// impossibility - two jobs that do not overlap on the clock but are further
/// apart on the road than the gap between them.
///
/// <para>
/// <b>Why this hangs off <see cref="IProviderAssignmentEligibilityService"/>
/// and not <see cref="IProviderScheduleConflictService"/>.</b> Travel time is
/// as physical as overlap, but the two differ in what they are made of.
/// Overlap is arithmetic over two rows this system wrote itself: it is certain,
/// it is free, and it is therefore checked unconditionally on every assignment
/// path, the manual admin one included. Travel time is an <i>estimate</i> from
/// a third party that costs money per lookup, degrades to an approximation
/// under failure, and cannot see that the provider's next job is their own
/// flat upstairs. Hard-refusing an admin who is looking at the real world -
/// and who already overrides <c>ProviderCapacity</c> by design (PROVIDER.md
/// OPEN DECISIONS - AUTOMATIC ASSIGNMENT #2) - on a number that may be wrong
/// is the wrong trade. So this is a candidate filter for the automatic engine,
/// behind <c>AutoAssignmentOptions.TravelBufferEnabled</c>, and the manual path
/// keeps exactly the one refusal it had before (overlap).
/// </para>
/// </summary>
public interface IProviderTravelFeasibilityService
{
    /// <summary>
    /// The adjacent same-day job the provider cannot make the drive to or from
    /// in time, or null when the day's shape is drivable (or the check is
    /// switched off).
    ///
    /// <para>
    /// Only the immediately adjacent jobs are considered - the last one ending
    /// at or before this slot starts, and the first one starting at or after it
    /// ends, on the same <c>SlotDate</c>. Anything further out in the day is
    /// separated by those neighbours' own checks, and anything overlapping is
    /// already <see cref="IProviderScheduleConflictService"/>'s refusal, not
    /// this one's.
    /// </para>
    ///
    /// <para>
    /// A leg is infeasible when <c>travel + buffer &gt; gap</c>. The buffer
    /// (<c>AutoAssignmentOptions.TravelHandoverBufferMinutes</c>) covers
    /// parking, finding the door and handover either side of a drive, so it is
    /// added only when there is a drive: a zero-length leg - the next job at
    /// the same address - needs no gap at all, which is what keeps task 288's
    /// back-to-back case legal.
    /// </para>
    ///
    /// <para>
    /// <b>It never fails open.</b> <see cref="IRouteEstimateProvider"/> already
    /// degrades to the sandbox estimator rather than returning nothing, and a
    /// sandbox figure is used here exactly like a measured one (unlike task
    /// 267's <i>ranking</i>, which ignores an all-sandbox response because it
    /// carries no information straight-line distance did not already have -
    /// here it carries the only information there is). If a response is
    /// unusable outright, the sandbox estimate is computed locally instead. A
    /// routing outage must not turn into a day of impossible schedules.
    /// </para>
    /// </summary>
    Task<ProviderTravelConflict?> FindConflictAsync(
        Guid providerId,
        Booking booking,
        CancellationToken cancellationToken = default);
}

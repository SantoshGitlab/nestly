using Nestly.Domain;

namespace Nestly.Application.ProviderManagement;

/// <summary>
/// Task 288: the other live job that stops a provider taking this one - the
/// booking they are already committed to, and the slot window that clashes,
/// so an admin can be told exactly what they collided with.
/// </summary>
public sealed record ProviderScheduleConflict(Guid BookingId, DateOnly SlotDate, TimeSpan StartTime, TimeSpan EndTime);

/// <summary>
/// Task 288: "one person cannot be in two places at once, and cannot do two
/// things at once." Deliberately separate from
/// <see cref="IProviderAssignmentEligibilityService"/>'s
/// <c>ProviderCapacity</c> limits: those are configurable policy (and
/// PROVIDER.md OPEN DECISIONS - AUTOMATIC ASSIGNMENT #2 lets manual admin
/// assignment treat them as advisory), whereas this is a physical invariant.
/// It therefore holds on every assignment path - the automatic engine and the
/// manual admin one alike - and holds whether or not the provider has a
/// <c>ProviderCapacity</c> row, which today none of them do (nothing in this
/// codebase can create one - see <c>IProviderCapacityRepository</c>).
/// </summary>
public interface IProviderScheduleConflictService
{
    /// <summary>
    /// The provider's earliest other live job overlapping <paramref name="booking"/>'s
    /// slot, or null when they are free to take it.
    ///
    /// "Live" is <c>Assigned</c>/<c>Accepted</c> only, mirroring
    /// <c>BookingProviderAssignmentStatus</c>'s own doc comment - a
    /// Rejected/Reassigned/Withdrawn row is nobody's commitment any more and
    /// must not block a new assignment.
    ///
    /// Overlap is half-open (<c>NewStart &lt; ExistingEnd &amp;&amp; ExistingStart &lt;
    /// NewEnd</c>): a slot occupies <c>[start, end)</c>, so back-to-back
    /// 09:00-11:00 and 11:00-13:00 jobs touch at an endpoint without
    /// overlapping and both stay legal, while any shared interior instant -
    /// partial overlap either way round, containment, or the identical slot -
    /// is a conflict.
    ///
    /// <paramref name="booking"/> itself is never its own conflict, which
    /// also excludes the assignment being superseded by a reassignment, since
    /// that row belongs to this same booking.
    /// </summary>
    Task<ProviderScheduleConflict?> FindConflictAsync(Guid providerId, Booking booking);
}

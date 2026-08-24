using Nestly.Domain;

namespace Nestly.Application.ProviderManagement;

/// <summary>
/// The handful of Booking/Assignment fields <see cref="IProviderJobOccupancyService"/>
/// needs, mirroring the anonymous projections its callers (<c>ProviderScheduleConflictService</c>,
/// <c>ProviderTravelFeasibilityService</c>) already select rather than loading
/// either full entity - both are cross-aggregate, provider-day-scoped reads
/// with no business need for anything else on either row.
/// </summary>
public sealed record JobOccupancy(
    BookingProviderAssignmentStatus AssignmentStatus,
    DateTime? CompletedAtUtc,
    bool IsDurationBasedSnapshot,
    DateOnly SlotDate,
    TimeSpan SlotStartTimeSnapshot,
    TimeSpan SlotEndTimeSnapshot);

/// <summary>
/// Computes when a job actually stops occupying a provider's schedule (the
/// provider-queue early-release model: "eligible for the next order
/// immediately after verified completion, subject to availability, travel,
/// buffer, service duration, and scheduling constraints"). Normally that is
/// just the booking's own slot end - the assumption every existing overlap/
/// travel check was built on - but a verified-complete
/// (<see cref="BookingProviderAssignmentStatus.Completed"/>), non-duration-based
/// job instead occupies the provider only until its <em>actual</em> finish
/// time: earlier than the slot's nominal end releases the provider early,
/// later (an overrun) correctly extends the block rather than
/// under-counting how long the provider was genuinely still there.
///
/// A duration-based service (<see cref="JobOccupancy.IsDurationBasedSnapshot"/>)
/// is deliberately exempt in both directions: the customer bought a block of
/// time, so the provider stays committed through the booked slot regardless
/// of when the checklist finished, and a duration-based overrun is not
/// reflected here either - extending that commitment is a separate,
/// explicit action (an admin/provider call), not something this inferred
/// from a timestamp.
/// </summary>
public interface IProviderJobOccupancyService
{
    /// <summary>
    /// The wall-clock time-of-day, on <see cref="JobOccupancy.SlotDate"/>,
    /// this job stops occupying the provider - for comparison against another
    /// same-day job's own slot times. A same-day <see cref="TimeSpan"/> cannot
    /// express "the next day"; see the implementation for how a genuine
    /// past-midnight overrun is handled.
    /// </summary>
    TimeSpan EffectiveEndTime(JobOccupancy job);
}

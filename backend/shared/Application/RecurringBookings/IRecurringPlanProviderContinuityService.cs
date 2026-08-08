namespace Nestly.Application.RecurringBookings;

/// <summary>
/// Task 297: who a recurring plan's <i>standing</i> provider is - the
/// professional the customer has an ongoing relationship with, as opposed to
/// whoever the matching engine would rank first for a booking it knows
/// nothing about.
///
/// A recurring plan is a standing customer relationship (tasks.csv row 300:
/// "so providers can plan around a standing customer relationship"), and the
/// plan itself deliberately stores no provider column - a plan describes the
/// schedule and nothing else (see <c>RecurringBookingPlan</c>'s doc comment),
/// and a stored preferred-provider field would be a second source of truth
/// that drifts the moment a provider is suspended, rejects a job, or is
/// reassigned away. So "standing provider" is derived from the plan's own
/// booking history instead, through task 296's
/// <c>Booking.RecurringBookingPlanId</c>.
///
/// Two callers, one rule: the generator uses it to decide whether an
/// occurrence needs the reassignment flow, and
/// <c>ProviderAutoAssignmentHandler</c> uses it to try that provider first
/// when the booking actually reaches assignment. They must agree, so neither
/// re-derives it.
/// </summary>
public interface IRecurringPlanProviderContinuityService
{
    /// <summary>
    /// The provider on the plan's most recent previous booking that still has
    /// one, or null when the plan has no history to continue (its first
    /// occurrence, or every prior occurrence lost its provider to a
    /// rejection). <paramref name="excludingBookingId"/> is the occurrence
    /// being placed right now - it must never count as its own precedent.
    /// </summary>
    Task<Guid?> FindStandingProviderAsync(Guid recurringBookingPlanId, Guid? excludingBookingId = null);
}

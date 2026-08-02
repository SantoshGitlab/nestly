namespace Nestly.Domain;

/// <summary>
/// The result the scheduler recorded for one scheduled date of a
/// <see cref="RecurringBookingPlan"/> (task 185). One row per
/// (plan, scheduled date) is written to <see cref="RecurringBookingOccurrence"/>
/// regardless of outcome - including <see cref="Booked"/> - so a re-run of
/// the Hangfire job (retries are expected to be safe re-executions, see
/// <c>BackgroundJobRegistration</c>) can tell "already handled" from "still
/// due" without re-attempting a booking that already succeeded.
/// </summary>
public enum RecurringBookingOccurrenceOutcome
{
    /// <summary>The orchestration created a real booking; <see cref="RecurringBookingOccurrence.BookingId"/> is set.</summary>
    Booked,

    /// <summary>The slot/address was no longer available at attempt time - not a hard error, the customer was notified and the plan moved on to its next date (PRODUCT-ENHANCEMENTS.md: "does not silently fail, and does not book a different slot without asking").</summary>
    SkippedSlotUnavailable,

    /// <summary>The booking orchestration itself rejected the attempt for a reason other than slot availability (e.g. the catalog service was deactivated, the address was deleted). Also skip-and-notify, tracked separately from <see cref="SkippedSlotUnavailable"/> for support/diagnostics.</summary>
    SkippedOrchestrationRejected
}

namespace Nestly.Domain;

/// <summary>
/// The result the scheduler recorded for one scheduled date of a
/// <see cref="RecurringBookingPlan"/> (task 185). One row per
/// (plan, scheduled date) is written to <see cref="RecurringBookingOccurrence"/>
/// regardless of outcome - including <see cref="Booked"/> - so a re-run of
/// the Hangfire job (retries are expected to be safe re-executions, see
/// <c>BackgroundJobRegistration</c>) can tell "already handled" from "still
/// due" without re-attempting a booking that already succeeded.
///
/// APPEND-ONLY. Stored as a string (<c>RecurringBookingOccurrenceConfiguration</c>
/// converts it, max length 30) but crossing the wire as its ordinal, the same
/// rule <see cref="NotificationEventType"/> and <see cref="BookingStatus"/>
/// document - a new member goes on the end, never between two existing ones.
/// </summary>
public enum RecurringBookingOccurrenceOutcome
{
    /// <summary>The orchestration created a real booking; <see cref="RecurringBookingOccurrence.BookingId"/> is set.</summary>
    Booked,

    /// <summary>The slot/address was no longer available at attempt time - not a hard error, the customer was notified and the plan moved on to its next date (PRODUCT-ENHANCEMENTS.md: "does not silently fail, and does not book a different slot without asking").</summary>
    SkippedSlotUnavailable,

    /// <summary>The booking orchestration itself rejected the attempt for a reason other than slot availability (e.g. the catalog service was deactivated, the address was deleted). Also skip-and-notify, tracked separately from <see cref="SkippedSlotUnavailable"/> for support/diagnostics.</summary>
    SkippedOrchestrationRejected,

    /// <summary>
    /// Task 297. A real booking was created (<see cref="RecurringBookingOccurrence.BookingId"/>
    /// is set, exactly like <see cref="Booked"/>) but the provider this plan
    /// has a standing relationship with - whoever served its most recent
    /// occurrence - cannot serve this date, and a different eligible provider
    /// can. The occurrence is handed to the existing reassignment flow rather
    /// than skipped: the booking exists, and <c>ProviderAutoAssignmentHandler</c>
    /// places the substitute when the booking reaches
    /// <see cref="BookingStatus.AwaitingFulfilment"/>, superseding any live
    /// assignment through <c>BookingProviderAssignment.MarkReassigned</c> so
    /// the customer is told their professional changed (task 295).
    /// </summary>
    BookedProviderReassigned,

    /// <summary>
    /// Task 297. A real booking was created, but at generation time no active
    /// provider at all was eligible for this date - not the plan's standing
    /// provider and not a substitute. Recorded rather than silent: the
    /// occurrence carries its reason, the customer is notified, and the
    /// booking sits in the manual admin assignment queue exactly where an
    /// unstaffable one-off booking already sits (PROVIDER.md OPEN DECISIONS -
    /// AUTOMATIC ASSIGNMENT #5, "no eligible candidate is not an error").
    ///
    /// Deliberately NOT a skip: the scheduler runs
    /// <c>RecurringBookingOptions.LeadTimeDays</c>
    /// ahead of the date precisely so a supply problem surfaces early, and
    /// supply that is short today may not be short on the day - throwing away
    /// the customer's slot over a forecast would be worse than flagging it.
    /// A date that genuinely cannot be booked at all still ends up as
    /// <see cref="SkippedSlotUnavailable"/>/<see cref="SkippedOrchestrationRejected"/>,
    /// because the orchestration refuses to create the booking in the first place.
    /// </summary>
    BookedProviderUnavailable
}

/// <summary>Task 297: keeps "did this outcome produce a booking row" in one place now that more than one outcome does.</summary>
public static class RecurringBookingOccurrenceOutcomeExtensions
{
    /// <summary>
    /// True for every outcome that materialized a real <see cref="Booking"/> -
    /// exactly the outcomes whose <see cref="RecurringBookingOccurrence.BookingId"/>
    /// must be set, and the ones that consume one of the plan's occurrences.
    /// </summary>
    public static bool CreatedBooking(this RecurringBookingOccurrenceOutcome outcome) => outcome
        is RecurringBookingOccurrenceOutcome.Booked
        or RecurringBookingOccurrenceOutcome.BookedProviderReassigned
        or RecurringBookingOccurrenceOutcome.BookedProviderUnavailable;
}

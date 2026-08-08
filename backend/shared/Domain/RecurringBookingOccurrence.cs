using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>
/// An immutable, append-only audit row of what the scheduler (task 185) did
/// for one scheduled date of a <see cref="RecurringBookingPlan"/> - mirrors
/// the append-only pattern already used for <c>BookingStatusHistory</c> and
/// <c>WalletLedgerEntry</c>. Modeled as its own entity with its own
/// repository (not a navigation collection on the plan) for two reasons:
/// (1) the scheduler's daily sweep needs to query "what's due across every
/// plan", a cross-aggregate query, not "what happened to this one plan";
/// (2) a long-running weekly plan accumulates one row a week for years, and
/// nothing that loads a plan for pause/cancel should have to pull that
/// history along with it.
///
/// The (RecurringBookingPlanId, ScheduledDate) pair is unique
/// (see <c>RecurringBookingOccurrenceConfiguration</c>) - that uniqueness is
/// the scheduler's idempotency guard. A Hangfire retry (or a second overlapping
/// run) re-reads this table before attempting a date and skips anything
/// already recorded here, satisfying the "jobs must be idempotent, a retry
/// re-runs the whole method" contract <c>BackgroundJobRegistration</c> documents.
/// </summary>
public class RecurringBookingOccurrence : Entity<Guid>
{
    public Guid RecurringBookingPlanId { get; private set; }

    public DateOnly ScheduledDate { get; private set; }

    public RecurringBookingOccurrenceOutcome Outcome { get; private set; }

    /// <summary>Set exactly for the outcomes that materialized a booking - see <see cref="RecurringBookingOccurrenceOutcomeExtensions.CreatedBooking"/>.</summary>
    public Guid? BookingId { get; private set; }

    /// <summary>
    /// Human-readable reason for a skip - never the raw exception/stack trace
    /// (docs/CODING-STANDARDS.md: never expose internals), same discipline
    /// <c>ExportJob.MarkFailed</c> already follows. Task 297 also uses it to
    /// record why a booked occurrence needed a substitute provider, or why
    /// none could be found - the field is the occurrence's one free-text
    /// "what happened here" slot and splitting it in two would leave every
    /// reader having to check both.
    /// </summary>
    public string? SkipReason { get; private set; }

    public DateTime ProcessedAtUtc { get; private set; }

    protected RecurringBookingOccurrence() { }

    public RecurringBookingOccurrence(
        Guid id,
        Guid recurringBookingPlanId,
        DateOnly scheduledDate,
        RecurringBookingOccurrenceOutcome outcome,
        Guid? bookingId,
        string? skipReason)
        : base(id)
    {
        if (outcome.CreatedBooking() && bookingId is null)
        {
            throw new ArgumentException("A booked occurrence must carry the booking id it created.", nameof(bookingId));
        }

        if (!outcome.CreatedBooking() && bookingId is not null)
        {
            throw new ArgumentException("Only a booked occurrence may carry a booking id.", nameof(bookingId));
        }

        RecurringBookingPlanId = recurringBookingPlanId;
        ScheduledDate = scheduledDate;
        Outcome = outcome;
        BookingId = bookingId;
        SkipReason = skipReason;
        ProcessedAtUtc = DateTime.UtcNow;
    }
}

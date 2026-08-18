namespace Nestly.Application.Bookings;

/// <summary>
/// Task 333's scheduled promotion: moves a paid, <c>Confirmed</c> booking to
/// <c>AwaitingFulfilment</c> once its slot is close enough to be worth
/// dispatching, which is the transition that puts it in front of the matching
/// engine (PROVIDER.md OPEN DECISIONS - AUTOMATIC ASSIGNMENT, decision 4).
///
/// <para>
/// The gap this closes: <c>ProviderAutoAssignmentHandler</c> has fired on
/// every transition <i>into</i> <c>AwaitingFulfilment</c> since task 246, but
/// nothing ever performed the <c>Confirmed -&gt; AwaitingFulfilment</c> one.
/// The state was reachable only by a reschedule or an assignment rejection, so
/// an ordinary booking sat <c>Confirmed</c> until an admin moved it by hand -
/// and the whole automatic dispatch stack behind it never ran. The QA sweep of
/// 2026-08-18 (docs/QA-REPORT-2026-08-18.md, finding 6) found admin-web's copy
/// describing automatic assignment that could therefore never happen.
/// </para>
///
/// <para>
/// Registered as a Hangfire recurring job the same way
/// <see cref="IBookingExpirySweepJob"/> is - the interface lives in
/// Application so Hangfire's activator can resolve it through DI - and
/// scheduled by <c>BookingFulfilmentPromotionJobScheduleExtensions</c>, whose
/// doc comment carries the cadence/lead-window reasoning.
/// </para>
/// </summary>
public interface IBookingFulfilmentPromotionJob
{
    /// <summary>
    /// Promotes every <c>Confirmed</c> booking whose slot starts within
    /// <c>AutoAssignmentOptions.PromotionLeadTimeHours</c>.
    ///
    /// <para>
    /// Idempotent and safe to re-run, which Hangfire's retry convention
    /// requires: a promoted booking is no longer <c>Confirmed</c>, so a second
    /// pass over the same data selects nothing. Bookings that are cancelled,
    /// expired, rescheduled, already assigned, or still awaiting payment are
    /// never candidates - the filter is <c>Confirmed</c> only, not "not
    /// terminal".
    /// </para>
    ///
    /// <para>
    /// One booking that cannot be promoted (it raced out of <c>Confirmed</c>,
    /// or its write failed) is logged and skipped; the rest of the pass
    /// continues.
    /// </para>
    /// </summary>
    /// <returns>How many bookings this pass promoted - usually a small number, and zero on a quiet system.</returns>
    Task<int> PromoteDueBookingsAsync(CancellationToken cancellationToken = default);
}

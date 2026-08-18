using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nestly.Application.Abstractions.Time;
using Nestly.Application.Bookings;
using Nestly.Domain;
using Nestly.Infrastructure.Options;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// See <see cref="IBookingFulfilmentPromotionJob"/> - task 333's missing
/// <c>Confirmed -&gt; AwaitingFulfilment</c> transition.
/// </summary>
/// <remarks>
/// <para>
/// <b>It transitions, it does not assign.</b> Every piece of dispatch logic
/// already exists and already hangs off <c>BookingStatusChangedEvent</c>
/// (<c>ProviderAutoAssignmentHandler</c>, tasks 246-248): ranking, the
/// eligibility gate, the rejection-retry chain and the
/// <see cref="AutoAssignmentOptions.Enabled"/> kill switch. This job's entire
/// job is to raise that event at the right moment, so there is exactly one
/// implementation of "who fulfils this booking" rather than a second one that
/// drifts.
/// </para>
/// <para>
/// <b>Business-local, not UTC.</b> A booking's slot is stored as a wall-clock
/// date plus time-of-day with no offset, so "is this slot within the next N
/// hours" is answered in business time via <see cref="IBusinessClock"/> - the
/// same abstraction cancellation and reschedule fee windows use. Measuring the
/// lead window against <c>DateTime.UtcNow</c> would shift it by the whole
/// timezone offset.
/// </para>
/// <para>
/// <b>Two halves of one window.</b> The database is asked only for the slot
/// <i>date</i>, and the time of day is applied here. Not an oversight:
/// <see cref="Booking.SlotStartTimeSnapshot"/> is a <c>TimeSpan</c>, and
/// ordering comparisons on it translate on PostgreSQL's <c>interval</c> but
/// not on the SQLite the test suite runs, so one query expressing the exact
/// instant would have to be raw SQL written twice. The rule stays in one
/// place, in plain C#; the cost is that some bookings later on the boundary
/// day are read and passed over.
/// </para>
/// <para>
/// <b>Paging that terminates.</b> A promoted booking drops out of the
/// candidate query (it is no longer <c>Confirmed</c>) but one that was passed
/// over or failed does not, so the offset is advanced past exactly those. The
/// pass therefore moves forward whatever happens to any individual booking and
/// cannot re-read the same page forever. <see cref="MaxPagesPerPass"/> bounds
/// one invocation without ever silently dropping a backlog: whatever is still
/// due is still due on the next pass, minutes later.
/// </para>
/// <para>
/// <b>Concurrency.</b> Hangfire's globally registered
/// <c>DisableConcurrentExecutionAttribute</c> (see
/// <c>BackgroundJobRegistration</c>) already prevents two passes overlapping.
/// It is not relied on for correctness: a booking moved out of
/// <c>Confirmed</c> by anything else - an admin, a cancellation, a second
/// pass - between this pass's read and its write is refused by
/// <see cref="BookingLifecycle"/> and skipped, so the worst case of a lost
/// race is a log line, never a double promotion.
/// </para>
/// </remarks>
public class BookingFulfilmentPromotionJob : IBookingFulfilmentPromotionJob
{
    /// <summary>
    /// The status-history reason an admin reads in the booking timeline. Says
    /// what moved it and why, because "Awaiting Fulfilment" appearing with no
    /// actor behind it is precisely the confusion this task was raised over.
    /// <para>
    /// Public, unlike the private reason constants on the sweeps beside this
    /// one, so the test suite asserts the string a real admin will read rather
    /// than a copy of it that can drift.
    /// </para>
    /// </summary>
    public const string PromotionReason = "Automatically queued for fulfilment as the slot approached.";

    /// <summary>
    /// Hard stop on one pass, so a large backlog (a first deploy, or a long
    /// outage) drains over several passes instead of one unbounded run holding
    /// a worker and a database connection open. Nothing is lost: whatever is
    /// still due is still due on the next pass, minutes later.
    /// </summary>
    private const int MaxPagesPerPass = 20;

    private readonly IBookingRepository _bookingRepository;
    private readonly IBusinessClock _businessClock;
    private readonly IOptions<AutoAssignmentOptions> _options;
    private readonly ILogger<BookingFulfilmentPromotionJob> _logger;

    public BookingFulfilmentPromotionJob(
        IBookingRepository bookingRepository,
        IBusinessClock businessClock,
        IOptions<AutoAssignmentOptions> options,
        ILogger<BookingFulfilmentPromotionJob> logger)
    {
        _bookingRepository = bookingRepository;
        _businessClock = businessClock;
        _options = options;
        _logger = logger;
    }

    public async Task<int> PromoteDueBookingsAsync(CancellationToken cancellationToken = default)
    {
        var options = _options.Value;
        if (!options.PromotionEnabled)
        {
            // Logged, unlike AutoAssignmentOptions.Enabled's silent branch:
            // this one runs on a timer rather than per booking, so it cannot
            // become noise, and "why has nothing been queued for fulfilment
            // since Tuesday" is an incident question that deserves an answer
            // in the log rather than only in the config.
            _logger.LogInformation("Booking fulfilment promotion is disabled (AutoAssignment:PromotionEnabled); no bookings were promoted.");
            return 0;
        }

        var cutoffLocal = _businessClock.Now.AddHours(options.PromotionLeadTimeHours);
        var cutoffDate = DateOnly.FromDateTime(cutoffLocal);
        var cutoffTimeOfDay = cutoffLocal.TimeOfDay;

        // Rows this pass has looked at and left Confirmed - bookings later on
        // the boundary day, plus any that failed. They stay in the candidate
        // query, so this offset is what carries the pass past them. Promoted
        // bookings are deliberately not counted here: they leave the query and
        // close the gap behind them on their own.
        var skipped = 0;
        int promoted = 0, failed = 0;

        for (var page = 0; page < MaxPagesPerPass; page++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var candidates = await _bookingRepository.ListConfirmedDueForFulfilmentAsync(
                cutoffDate, skipped, options.PromotionBatchSize);
            if (candidates.Count == 0)
            {
                break;
            }

            foreach (var booking in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!IsWithinLeadWindow(booking, cutoffDate, cutoffTimeOfDay))
                {
                    skipped++;
                    continue;
                }

                if (await TryPromoteAsync(booking, cancellationToken))
                {
                    promoted++;
                }
                else
                {
                    failed++;
                    skipped++;
                }
            }

            if (candidates.Count < options.PromotionBatchSize)
            {
                break;
            }
        }

        if (promoted > 0 || failed > 0)
        {
            _logger.LogInformation(
                "Booking fulfilment promotion: {PromotedCount} booking(s) moved to AwaitingFulfilment within the {LeadTimeHours}h lead window, {FailedCount} skipped.",
                promoted, options.PromotionLeadTimeHours, failed);
        }

        return promoted;
    }

    /// <summary>
    /// The precise half of the window the database could not express: a
    /// candidate on an earlier slot date is always due, one on the boundary
    /// date only if it starts by the cutoff time of day. Both sides are
    /// business-local wall clock, which is the only frame in which comparing
    /// them means anything.
    /// </summary>
    private static bool IsWithinLeadWindow(Booking booking, DateOnly cutoffDate, TimeSpan cutoffTimeOfDay) =>
        booking.SlotDate < cutoffDate || booking.SlotStartTimeSnapshot <= cutoffTimeOfDay;

    /// <summary>
    /// One booking's promotion, isolated: nothing it can do stops the pass.
    ///
    /// <para>
    /// The broad catch is the failure-isolation requirement itself, and
    /// <see cref="IBookingRepository.DiscardChanges"/> is what makes it honest.
    /// Catching alone would not be enough - the transition has already been
    /// applied in memory by the time anything can throw, and the next
    /// booking's successful save would flush it, quietly promoting a booking
    /// this method just reported as failed, non-deterministically depending on
    /// which rows the page happened to contain. Discarding leaves the failed
    /// booking exactly as it was found, <c>Confirmed</c>, for the next pass.
    /// </para>
    ///
    /// <para>
    /// Cancellation is the one thing that must propagate: it means the process
    /// is shutting down, not that this booking is bad.
    /// </para>
    /// </summary>
    private async Task<bool> TryPromoteAsync(Booking booking, CancellationToken cancellationToken)
    {
        try
        {
            // Re-checked against the entity actually loaded, not trusted from
            // the query: this is the guard that makes a lost race a no-op.
            if (booking.Status != BookingStatus.Confirmed)
            {
                return false;
            }

            booking.TransitionTo(BookingStatus.AwaitingFulfilment, PromotionReason);
            await _bookingRepository.UpdateAsync(booking);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Booking {BookingId} failed to promote to AwaitingFulfilment; leaving it Confirmed and continuing with the rest of the sweep.",
                booking.Id);

            _bookingRepository.DiscardChanges(booking);
            return false;
        }
    }
}

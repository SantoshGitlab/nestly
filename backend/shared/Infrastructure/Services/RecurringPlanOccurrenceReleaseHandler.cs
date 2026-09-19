using MediatR;
using Microsoft.Extensions.Logging;
using Nestly.Application.Bookings;
using Nestly.Application.RecurringBookings;
using Nestly.Domain;
using Nestly.Domain.Events;
using Nestly.Infrastructure.Persistence.Interceptors;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// Occurrence-count integrity fix: gives a recurring plan's occurrence
/// budget back once a generated booking is confirmed to have never been
/// delivered - see <see cref="RecurringBookingPlan.ReleaseOccurrence"/>'s doc
/// comment for what that unlocks. A fifth independent handler on
/// <see cref="BookingStatusChangedEvent"/>, same shape as
/// <see cref="EscrowReleaseOnCompletionHandler"/>/<see cref="ReferralCancellationFraudSignalHandler"/>/
/// <see cref="ReferralQualifyingBookingHandler"/>.
///
/// <para>
/// <b>Trigger set, and why <see cref="BookingStatus.Refunded"/> needs a
/// second check.</b> <c>BookingLifecycle</c>'s own transition table shows
/// <see cref="BookingStatus.CancelledByCustomer"/>/<see cref="BookingStatus.CancelledByAdmin"/>/
/// <see cref="BookingStatus.Expired"/> are each reachable only from a
/// pre-visit state - nothing transitions to any of them from
/// <see cref="BookingStatus.Completed"/> or <see cref="BookingStatus.RefundPending"/>
/// - so reaching one of those three always means the visit never happened,
/// and the occurrence is released unconditionally.
/// </para>
///
/// <para>
/// <see cref="BookingStatus.Refunded"/> is reachable from <i>two</i> different
/// origins the event payload alone cannot tell apart: directly from a
/// cancelled status (the booking never reached the visit) and from
/// <see cref="BookingStatus.Completed"/> via <see cref="BookingStatus.RefundPending"/>
/// (the visit happened, and this is a later quality-dispute refund of the
/// money only). Releasing the occurrence for the second case would be wrong
/// - a professional did the job, so it must still count against the plan's
/// budget regardless of what happened to the payment afterward. The
/// disambiguator is the booking's own status history: if
/// <see cref="BookingStatus.Completed"/> never appears in it, this refund
/// came from a pre-visit cancellation and the occurrence is released; if it
/// does appear, the occurrence stands.
/// </para>
/// </summary>
public sealed class RecurringPlanOccurrenceReleaseHandler : INotificationHandler<DomainEventNotification<BookingStatusChangedEvent>>
{
    private readonly IBookingRepository _bookingRepository;
    private readonly IRecurringBookingPlanRepository _planRepository;
    private readonly ILogger<RecurringPlanOccurrenceReleaseHandler> _logger;

    public RecurringPlanOccurrenceReleaseHandler(
        IBookingRepository bookingRepository,
        IRecurringBookingPlanRepository planRepository,
        ILogger<RecurringPlanOccurrenceReleaseHandler> logger)
    {
        _bookingRepository = bookingRepository;
        _planRepository = planRepository;
        _logger = logger;
    }

    public async Task Handle(DomainEventNotification<BookingStatusChangedEvent> notification, CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;
        if (domainEvent.ToStatus is not (BookingStatus.CancelledByCustomer or BookingStatus.CancelledByAdmin
            or BookingStatus.Expired or BookingStatus.Refunded))
        {
            return;
        }

        var booking = await _bookingRepository.GetByIdAsync(domainEvent.BookingId);
        if (booking?.RecurringBookingPlanId is not { } planId)
        {
            return;
        }

        if (domainEvent.ToStatus == BookingStatus.Refunded
            && booking.StatusHistory.Any(h => h.ToStatus == BookingStatus.Completed))
        {
            // The visit happened; this refund is a later, money-only dispute
            // resolution and must not give the occurrence back.
            return;
        }

        var plan = await _planRepository.GetByIdAsync(planId);
        if (plan is null)
        {
            _logger.LogWarning(
                "Booking {BookingId} reached {ToStatus} but its recurring plan {PlanId} no longer exists; skipping occurrence release.",
                booking.Id, domainEvent.ToStatus, planId);
            return;
        }

        plan.ReleaseOccurrence();
        await _planRepository.UpdateAsync(plan);
    }
}

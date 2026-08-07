using MediatR;
using Nestly.Application.Tracking;
using Nestly.Domain;
using Nestly.Domain.Events;
using Nestly.Infrastructure.Persistence.Interceptors;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// Takes a booking's arrival estimate away the moment the job stops being
/// trackable (task 271) - completion, either cancellation, expiry, a
/// reschedule, or an admin pulling the assignment.
/// </summary>
/// <remarks>
/// <para>
/// An event handler rather than a call in each of those services, which is the
/// opposite of how the two *computing* triggers are wired (the location-ingest
/// path and the en-route transition both call
/// <see cref="IBookingEtaService.RefreshAsync"/> outright). The asymmetry is
/// deliberate: there are exactly two places an ETA is worth computing and they
/// are named in the feature, whereas a booking can leave the trackable states
/// from half a dozen services - completion, customer cancellation, admin
/// cancellation, reschedule, expiry sweep - and any one of them forgotten now
/// or added later would leave a finished job advertising an arrival time.
/// Subscribing to the transition itself is the only version of this rule that
/// cannot be forgotten.
/// </para>
/// <para>
/// <see cref="BookingStatusChangedEvent"/> rather than the narrower task 272
/// tracking events, for the same reason
/// <see cref="BookingNotificationTriggerHandler"/> uses it: this rule is about
/// the lifecycle as a whole, and the tracking family has no "job ended" member
/// to subscribe to. It reads <see cref="BookingStatusChangedEvent.FromStatus"/>
/// as well as the destination so it costs a query only on the one transition
/// per booking that actually crosses the boundary, not on every status change
/// in the system.
/// </para>
/// </remarks>
public sealed class BookingEtaSuppressionHandler : INotificationHandler<DomainEventNotification<BookingStatusChangedEvent>>
{
    private readonly IBookingEtaService _etaService;

    public BookingEtaSuppressionHandler(IBookingEtaService etaService)
    {
        _etaService = etaService;
    }

    public Task Handle(DomainEventNotification<BookingStatusChangedEvent> notification, CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;

        bool leftTrackableStates =
            BookingLifecycle.IsTrackable(domainEvent.FromStatus) &&
            !BookingLifecycle.IsTrackable(domainEvent.ToStatus);

        return leftTrackableStates
            ? _etaService.ClearAsync(domainEvent.BookingId, cancellationToken)
            : Task.CompletedTask;
    }
}

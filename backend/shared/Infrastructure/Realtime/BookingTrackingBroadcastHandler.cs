using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Nestly.Application.Tracking;
using Nestly.Domain.Events;
using Nestly.Infrastructure.Persistence.Interceptors;

namespace Nestly.Infrastructure.Realtime;

/// <summary>
/// Pushes the three live tracking events into the watching booking's SignalR
/// group (task 274) - modelled on <see cref="ChatHubBroadcastHandler"/>, which
/// does the same job for chat. Runs in whichever process committed the change;
/// the Redis backplane (see <see cref="BookingTrackingHub"/>) is what makes
/// <see cref="IHubContext{BookingTrackingHub}.Clients"/> reach connections held
/// by the other API processes - which is the entire point here, because the
/// provider's location ping is ingested by provider-api and has to land on a
/// customer connection held by consumer-api.
///
/// One class for all three events rather than three classes, the shape
/// <c>BookingNotificationTriggerHandler</c> already uses: they share a single
/// non-obvious rule (the swallow below) and a single group-naming decision, and
/// splitting them would triplicate both.
/// </summary>
/// <remarks>
/// <para>
/// <b>Payloads are the narrow <c>...Broadcast</c> records from
/// <c>Nestly.Application.Tracking</c>, never the domain events.</b> See that
/// file for the PII rule; the short version is that a SignalR frame reaches
/// every connection in the group, so a provider's phone number on one of these
/// would be a privacy incident, not a bug.
/// </para>
/// <para>
/// <b>Every broadcast swallows its own failure.</b> Dispatch is in-process,
/// post-commit MediatR with no outbox (docs/ARCHITECTURE.md, "DOMAIN EVENT
/// DISPATCH AND DELIVERY"), so a handler that throws propagates out of
/// <c>SaveChangesAsync</c> to the caller <i>after</i> the data is already
/// committed: the write succeeded and the request reports failure. A provider
/// would see their location ping rejected because Redis was briefly
/// unreachable. That trade is settled in the architecture doc - a lost
/// tracking broadcast is explicitly acceptable because
/// <c>GET /api/v1/bookings/{bookingId}/tracking</c> stays the source of truth
/// and the client re-reads it on reconnect. Hence no retry, no queue and no
/// dead-letter here: this is the fast path, not the only path.
/// </para>
/// </remarks>
public sealed class BookingTrackingBroadcastHandler :
    INotificationHandler<DomainEventNotification<BookingStatusChangedEvent>>,
    INotificationHandler<DomainEventNotification<ProviderLocationUpdatedEvent>>,
    INotificationHandler<DomainEventNotification<BookingEtaUpdatedEvent>>
{
    /// <summary>Client-side SignalR event name booking lifecycle moves arrive under.</summary>
    public const string BookingStatusChangedMethod = "BookingStatusChanged";

    /// <summary>Client-side SignalR event name provider position fixes arrive under.</summary>
    public const string ProviderLocationUpdatedMethod = "ProviderLocationUpdated";

    /// <summary>Client-side SignalR event name arrival-estimate changes arrive under.</summary>
    public const string EtaUpdatedMethod = "EtaUpdated";

    private readonly IHubContext<BookingTrackingHub> _hubContext;
    private readonly ILogger<BookingTrackingBroadcastHandler> _logger;

    public BookingTrackingBroadcastHandler(
        IHubContext<BookingTrackingHub> hubContext,
        ILogger<BookingTrackingBroadcastHandler> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <summary>
    /// Every status change is broadcast, with no
    /// <c>BookingLifecycle.IsTrackable</c> filter. The group is the filter -
    /// nobody is joined to a booking they cannot watch - and the transitions a
    /// watcher most needs are precisely the ones that leave the trackable
    /// window (Completed, CancelledByProvider), which an IsTrackable(ToStatus)
    /// test would suppress exactly when the screen has to stop showing a
    /// provider en route.
    /// </summary>
    public Task Handle(DomainEventNotification<BookingStatusChangedEvent> notification, CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;

        return BroadcastAsync(
            domainEvent.BookingId,
            BookingStatusChangedMethod,
            new BookingStatusChangedBroadcast(domainEvent.BookingId, domainEvent.FromStatus, domainEvent.ToStatus),
            cancellationToken);
    }

    /// <summary>
    /// A fix with no booking is an idle/on-shift ping (see
    /// <see cref="ProviderLocationUpdatedEvent.BookingId"/>) and belongs to no
    /// tracking group at all. Broadcasting it would mean either inventing a
    /// group name from a provider id - putting a provider's movements on a
    /// channel a customer could join - or sending to a group named after
    /// <see cref="Guid.Empty"/>. Both are wrong; there is simply nothing to do.
    /// </summary>
    public Task Handle(DomainEventNotification<ProviderLocationUpdatedEvent> notification, CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;
        if (domainEvent.BookingId is not { } bookingId)
        {
            return Task.CompletedTask;
        }

        return BroadcastAsync(
            bookingId,
            ProviderLocationUpdatedMethod,
            new ProviderLocationBroadcast(
                bookingId, domainEvent.Latitude, domainEvent.Longitude, domainEvent.RecordedAtUtc),
            cancellationToken);
    }

    /// <summary>
    /// No throttle here. Task 271 raises
    /// <see cref="BookingEtaUpdatedEvent"/> only on a material change (past its
    /// 60s/250m thresholds) and <c>ClearEta</c> raises nothing at all, so this
    /// stream is already de-noised upstream; a second throttle would only make
    /// the two disagree about what the customer is currently being shown.
    /// </summary>
    public Task Handle(DomainEventNotification<BookingEtaUpdatedEvent> notification, CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;

        return BroadcastAsync(
            domainEvent.BookingId,
            EtaUpdatedMethod,
            new BookingEtaBroadcast(domainEvent.BookingId, domainEvent.EtaSeconds, domainEvent.EtaComputedAtUtc),
            cancellationToken);
    }

    private async Task BroadcastAsync(
        Guid bookingId,
        string method,
        object payload,
        CancellationToken cancellationToken)
    {
        try
        {
            await _hubContext.Clients
                .Group(TrackingGroups.Booking(bookingId))
                .SendAsync(method, payload, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Shutdown or an abandoned request, not a broadcast failure. Let it
            // out: swallowing it would turn a cooperative cancellation into a
            // silent one and hide it from the caller that asked for it.
            throw;
        }
        catch (Exception exception)
        {
            // Booking id and method name only - the payload is never logged.
            // It carries no PII by construction, but this handler is the one
            // place that sees all three of them and would be the obvious place
            // for a future payload's contents to leak into a log file
            // (docs/CODING-STANDARDS.md LOGGING).
            _logger.LogWarning(
                exception,
                "Tracking broadcast {BroadcastMethod} failed for booking {BookingId}; the client will recover on its next tracking read.",
                method,
                bookingId);
        }
    }
}

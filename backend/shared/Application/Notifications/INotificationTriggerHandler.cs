using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Application.Notifications;

/// <summary>
/// Implemented by the four notification trigger handlers so the sweep can
/// re-invoke one of them directly (task 294).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why the sweep does not simply re-publish the domain event.</b>
/// Re-publishing a <c>BookingStatusChangedEvent</c> through MediatR would also
/// re-run every other subscriber of that stream - escrow release, referral
/// qualification, metrics, auto-assignment - none of which is idempotent and
/// several of which move money. The retry path must reach the notification
/// handlers and nothing else, so it addresses them through an interface only
/// they implement.
/// </para>
/// <para>
/// Non-generic on purpose: the sweep holds an <see cref="IDomainEvent"/> whose
/// concrete type it learned from a string in the database, so a generic
/// interface would only be reachable by reflection. Each implementation
/// pattern-matches its own event types and ignores the rest.
/// </para>
/// </remarks>
public interface INotificationTriggerHandler
{
    /// <summary>Whether this handler is the one that owns <paramref name="domainEventType"/>. Exactly one handler answers true for any event the planner plans for; none does for anything else.</summary>
    bool CanHandle(Type domainEventType);

    /// <summary>Runs the same body the in-process MediatR path runs, against a rehydrated event. Deduplication is the coordinator's job, not this method's.</summary>
    Task HandleAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default);
}

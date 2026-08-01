using MediatR;
using Nestly.Application.Abstractions.Observability;
using Nestly.Domain.Events;
using Nestly.Infrastructure.Persistence.Interceptors;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// Records every booking lifecycle status transition as a metric (task 137b,
/// SRS 29.6). Wired the same way <see cref="EscrowReleaseOnCompletionHandler"/>
/// and <see cref="BookingNotificationTriggerHandler"/> react to
/// <see cref="BookingStatusChangedEvent"/> - <c>Booking.TransitionTo</c> is the
/// single place every status change in the platform goes through (creation,
/// payment success/failure, cancellation, reschedule, refund, completion),
/// so this one handler captures the full transition graph without needing to
/// be wired into each of those call sites individually.
/// </summary>
public sealed class BookingMetricsHandler : INotificationHandler<DomainEventNotification<BookingStatusChangedEvent>>
{
    private readonly IMetricsService _metricsService;

    public BookingMetricsHandler(IMetricsService metricsService) => _metricsService = metricsService;

    public Task Handle(DomainEventNotification<BookingStatusChangedEvent> notification, CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;
        _metricsService.RecordBookingStatusTransition(domainEvent.FromStatus.ToString(), domainEvent.ToStatus.ToString());
        return Task.CompletedTask;
    }
}

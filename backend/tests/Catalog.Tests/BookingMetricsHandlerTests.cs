using FluentAssertions;
using Nestly.Application.Abstractions.Observability;
using Nestly.Domain;
using Nestly.Domain.Events;
using Nestly.Infrastructure.Persistence.Interceptors;
using Nestly.Infrastructure.Services;

namespace Nestly.Catalog.Tests;

/// <summary>
/// Covers task 137b's status-transition tracking: <see cref="BookingMetricsHandler"/>
/// forwards every <see cref="BookingStatusChangedEvent"/> it receives to
/// <see cref="IMetricsService.RecordBookingStatusTransition"/>, regardless of
/// which status pair it is - it doesn't filter by ToStatus the way
/// BookingNotificationTriggerHandler does, since every transition is
/// operationally interesting for this metric, not just the ones with a
/// customer-facing notification.
/// </summary>
public sealed class BookingMetricsHandlerTests
{
    [Theory]
    [InlineData(BookingStatus.Initiated, BookingStatus.PaymentPending)]
    [InlineData(BookingStatus.PaymentPending, BookingStatus.Confirmed)]
    [InlineData(BookingStatus.PaymentPending, BookingStatus.PaymentFailed)]
    [InlineData(BookingStatus.InProgress, BookingStatus.Completed)]
    public async Task Handle_records_the_from_and_to_status_of_every_transition(BookingStatus from, BookingStatus to)
    {
        var recorder = new RecordingMetricsService();
        var handler = new BookingMetricsHandler(recorder);

        await handler.Handle(
            new DomainEventNotification<BookingStatusChangedEvent>(new BookingStatusChangedEvent(Guid.NewGuid(), from, to)),
            CancellationToken.None);

        recorder.Transitions.Should().ContainSingle(t => t.From == from.ToString() && t.To == to.ToString());
    }

    private sealed class RecordingMetricsService : IMetricsService
    {
        public List<(string From, string To)> Transitions { get; } = [];

        public void RecordPaymentOutcome(bool succeeded, TimeSpan processingDuration, string? failureReason = null)
        {
        }

        public void RecordBookingCreated(bool succeeded, string? failureReason = null)
        {
        }

        public void RecordBookingStatusTransition(string fromStatus, string toStatus) => Transitions.Add((fromStatus, toStatus));

        public void RecordSlotConflict()
        {
        }

        public void RecordNotificationOutcome(string channel, bool succeeded, string? failureReason = null)
        {
        }
    }
}

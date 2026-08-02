using MediatR;
using Nestly.Application.NestlyCoins;
using Nestly.Domain;
using Nestly.Domain.Events;
using Nestly.Infrastructure.Persistence.Interceptors;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// Task 201 (docs/NESTLY-COINS.md "FRAUD / ABUSE PREVENTION"): if a booking
/// that already credited Nestly Coins is cancelled or refunded, reverses
/// the credit via an explicit debit within the program's clawback window.
/// Independent handler on BookingStatusChangedEvent, same shape as
/// ReferralCancellationFraudSignalHandler - reacts to the event on its own
/// rather than the qualifying-order handler needing to know about
/// cancellations.
/// </summary>
public sealed class NestlyCoinsClawbackHandler : INotificationHandler<DomainEventNotification<BookingStatusChangedEvent>>
{
    private readonly INestlyCoinsService _coinsService;

    public NestlyCoinsClawbackHandler(INestlyCoinsService coinsService)
    {
        _coinsService = coinsService;
    }

    public async Task Handle(DomainEventNotification<BookingStatusChangedEvent> notification, CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;
        if (domainEvent.ToStatus is not (BookingStatus.CancelledByCustomer or BookingStatus.CancelledByAdmin or BookingStatus.Refunded))
        {
            return;
        }

        await _coinsService.ClawbackOnCancellationAsync(domainEvent.BookingId);
    }
}

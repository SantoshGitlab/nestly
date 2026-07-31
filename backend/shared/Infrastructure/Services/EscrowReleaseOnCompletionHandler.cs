using MediatR;
using Microsoft.Extensions.Logging;
using Nestly.Application.Escrow;
using Nestly.Application.Payments;
using Nestly.Domain;
using Nestly.Domain.Events;
using Nestly.Infrastructure.Persistence.Interceptors;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// Releases a booking's held escrow to its provider as soon as it reaches
/// <see cref="BookingStatus.Completed"/> (task 158) - the counterpart to
/// <see cref="PaymentWebhookService"/> moving the payment into escrow at
/// confirmation time. Wired the same way <c>CatalogCacheInvalidationHandler</c>
/// reacts to catalog events (task 49): whatever code eventually drives a
/// booking to Completed (fulfilment/provider flows are Phase 5/8, not built
/// yet) gets this settlement for free, without those flows needing to know
/// escrow exists.
///
/// There is no Provider identity in the domain yet (deferred to Phase
/// 8/Partner), so this always releases with a null ProviderId placeholder -
/// see <see cref="IEscrowService.ReleaseToProviderAsync"/>'s doc comment.
/// </summary>
public sealed class EscrowReleaseOnCompletionHandler : INotificationHandler<DomainEventNotification<BookingStatusChangedEvent>>
{
    private readonly IPaymentTransactionRepository _paymentRepository;
    private readonly IEscrowService _escrowService;
    private readonly ILogger<EscrowReleaseOnCompletionHandler> _logger;

    public EscrowReleaseOnCompletionHandler(
        IPaymentTransactionRepository paymentRepository,
        IEscrowService escrowService,
        ILogger<EscrowReleaseOnCompletionHandler> logger)
    {
        _paymentRepository = paymentRepository;
        _escrowService = escrowService;
        _logger = logger;
    }

    public async Task Handle(DomainEventNotification<BookingStatusChangedEvent> notification, CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;
        if (domainEvent.ToStatus != BookingStatus.Completed)
        {
            return;
        }

        var transaction = await _paymentRepository.GetByBookingIdAsync(domainEvent.BookingId);
        if (transaction is null || transaction.Status != PaymentTransactionStatus.Success || transaction.CommissionAmount is null)
        {
            // Data-integrity gap, not a business outcome - a booking cannot
            // legally reach Completed without having gone through Confirmed
            // first, which is exactly where the transaction/commission are
            // recorded. Logged rather than thrown: this runs as a fire-and-
            // forget domain event handler, not inside the caller's own unit
            // of work.
            _logger.LogWarning(
                "Booking {BookingId} reached Completed with no successful, commission-recorded payment transaction - skipping escrow release.",
                domainEvent.BookingId);
            return;
        }

        await _escrowService.ReleaseToProviderAsync(
            domainEvent.BookingId, transaction.Id, providerId: null, transaction.CommissionAmount.Value);
    }
}

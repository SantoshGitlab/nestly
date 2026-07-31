using MediatR;
using Microsoft.Extensions.Logging;
using Nestly.Application;
using Nestly.Application.Bookings;
using Nestly.Application.Cancellations;
using Nestly.Application.Notifications;
using Nestly.Application.Payments;
using Nestly.Application.Refunds;
using Nestly.Domain;
using Nestly.Domain.Events;
using Nestly.Infrastructure.Persistence.Interceptors;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// Notification trigger wiring for booking-lifecycle events (SRS 19.1, tasks
/// 88b-f: booking confirmed, payment success/failure, cancellation,
/// reschedule, refund). Wired the same way <see cref="EscrowReleaseOnCompletionHandler"/>
/// reacts to <see cref="BookingStatusChangedEvent"/> - one handler, branching
/// on <see cref="BookingStatusChangedEvent.ToStatus"/>, rather than a
/// separate domain event per trigger: every one of these triggers already
/// corresponds to exactly one booking status transition in the existing
/// Booking/Payment/Refund/Cancellation/Reschedule services, so reusing that
/// single event stream avoids adding parallel, easy-to-miss event types.
///
/// Payment confirmation is the one place two distinct SRS 19.1 triggers
/// (BookingConfirmed and PaymentSuccess) share one underlying transition
/// (PaymentPending -> Confirmed, in PaymentWebhookService) - both are
/// dispatched from that single ToStatus == Confirmed branch.
/// </summary>
public sealed class BookingNotificationTriggerHandler : INotificationHandler<DomainEventNotification<BookingStatusChangedEvent>>
{
    private readonly ICustomerRepository _customerRepository;
    private readonly IBookingRepository _bookingRepository;
    private readonly IPaymentTransactionRepository _paymentRepository;
    private readonly ICancellationRepository _cancellationRepository;
    private readonly IRefundTransactionRepository _refundRepository;
    private readonly INotificationDispatchService _notificationDispatchService;
    private readonly ILogger<BookingNotificationTriggerHandler> _logger;

    public BookingNotificationTriggerHandler(
        ICustomerRepository customerRepository,
        IBookingRepository bookingRepository,
        IPaymentTransactionRepository paymentRepository,
        ICancellationRepository cancellationRepository,
        IRefundTransactionRepository refundRepository,
        INotificationDispatchService notificationDispatchService,
        ILogger<BookingNotificationTriggerHandler> logger)
    {
        _customerRepository = customerRepository;
        _bookingRepository = bookingRepository;
        _paymentRepository = paymentRepository;
        _cancellationRepository = cancellationRepository;
        _refundRepository = refundRepository;
        _notificationDispatchService = notificationDispatchService;
        _logger = logger;
    }

    public async Task Handle(DomainEventNotification<BookingStatusChangedEvent> notification, CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;

        // Not every transition has a notification - Initiated/PaymentPending/
        // AwaitingFulfilment/Assigned/InProgress/RefundPending are all silent.
        var eventTypes = domainEvent.ToStatus switch
        {
            BookingStatus.Confirmed => [NotificationEventType.BookingConfirmed, NotificationEventType.PaymentSuccess],
            BookingStatus.PaymentFailed => [NotificationEventType.PaymentFailed],
            BookingStatus.CancelledByCustomer or BookingStatus.CancelledByAdmin => [NotificationEventType.BookingCancelled],
            BookingStatus.Rescheduled => [NotificationEventType.BookingRescheduled],
            BookingStatus.Refunded => [NotificationEventType.RefundProcessed],
            _ => Array.Empty<NotificationEventType>()
        };

        if (eventTypes.Length == 0)
        {
            return;
        }

        var booking = await _bookingRepository.GetByIdAsync(domainEvent.BookingId);
        if (booking is null)
        {
            _logger.LogWarning("Booking {BookingId} not found while dispatching notifications for {ToStatus}.", domainEvent.BookingId, domainEvent.ToStatus);
            return;
        }

        var customer = await _customerRepository.GetByIdAsync(booking.CustomerId);
        var recipient = new NotificationRecipient(customer?.Mobile ?? booking.CustomerMobileSnapshot, customer?.Email);

        foreach (var eventType in eventTypes)
        {
            var variables = await BuildVariablesAsync(eventType, booking, cancellationToken);
            await _notificationDispatchService.DispatchAsync(booking.CustomerId, eventType, recipient, variables, bookingId: booking.Id, cancellationToken: cancellationToken);
        }
    }

    private async Task<Dictionary<string, string>> BuildVariablesAsync(NotificationEventType eventType, Booking booking, CancellationToken cancellationToken)
    {
        var variables = new Dictionary<string, string>
        {
            ["CustomerName"] = booking.CustomerNameSnapshot,
            ["BookingId"] = booking.Id.ToString(),
            ["ServiceName"] = booking.Items.Count > 0 ? booking.Items[0].NameSnapshot : string.Empty,
            ["SlotDate"] = booking.SlotDate.ToString("yyyy-MM-dd"),
            ["SlotWindow"] = booking.SlotWindowNameSnapshot,
            ["TotalPayable"] = booking.TotalPayableSnapshot.ToString("0.00")
        };

        switch (eventType)
        {
            case NotificationEventType.PaymentSuccess:
            case NotificationEventType.PaymentFailed:
            {
                var payment = await _paymentRepository.GetByBookingIdAsync(booking.Id);
                variables["Amount"] = (payment?.Amount ?? booking.TotalPayableSnapshot).ToString("0.00");
                break;
            }
            case NotificationEventType.BookingCancelled:
            {
                var cancellation = await _cancellationRepository.GetByBookingIdAsync(booking.Id);
                variables["CancellationFee"] = (cancellation?.CancellationFeeAmount ?? 0m).ToString("0.00");
                variables["RefundAmount"] = (cancellation?.RefundAmount ?? 0m).ToString("0.00");
                break;
            }
            case NotificationEventType.RefundProcessed:
            {
                var refunds = await _refundRepository.ListByBookingAsync(booking.Id);
                var latest = refunds.OrderByDescending(r => r.CreatedAtUtc).FirstOrDefault();
                variables["Amount"] = (latest?.Amount ?? 0m).ToString("0.00");
                variables["Method"] = latest?.Method.ToString() ?? string.Empty;
                break;
            }
        }

        return variables;
    }
}

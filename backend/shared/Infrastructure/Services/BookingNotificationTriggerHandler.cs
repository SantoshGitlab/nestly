using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nestly.Application;
using Nestly.Application.Bookings;
using Nestly.Application.Cancellations;
using Nestly.Application.Notifications;
using Nestly.Application.Payments;
using Nestly.Application.Refunds;
using Nestly.BuildingBlocks.Privacy;
using Nestly.Domain;
using Nestly.Domain.Events;
using Nestly.Infrastructure.Options;
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
///
/// <para>
/// <b>Task 276 - the fulfilment half.</b> Assigned/ProviderEnRoute/
/// ProviderArrived/InProgress/Completed used to map to nothing, so the entire
/// second half of a booking was silent. All five now dispatch, and all five
/// are triggered from <see cref="BookingStatusChangedEvent"/> rather than from
/// task 272's <c>ProviderEnRouteEvent</c>/<c>ProviderArrivedEvent</c>/
/// <c>ProviderAssignmentAcceptedEvent</c>. Reasons, in order of weight:
/// </para>
/// <list type="number">
/// <item>Every one of the five is a single <see cref="Booking.TransitionTo"/>
/// call, which raises exactly one <see cref="BookingStatusChangedEvent"/> - so
/// "exactly once per transition" is a property of the event stream, not
/// something this handler has to enforce.</item>
/// <item>The tracking events cover only two of the five, so mixing sources
/// would mean two handlers, two sets of repository reads and two places to
/// forget a new state.</item>
/// <item>Booking.TransitionTo raises the tracking event <i>in addition to</i>
/// BookingStatusChangedEvent, never instead of it (see its doc comment).
/// Subscribing to both would send "on the way" twice.</item>
/// </list>
/// <para>
/// <b>What ProviderAssigned actually means.</b> It fires on
/// AwaitingFulfilment -&gt; Assigned, which
/// <c>BookingProviderAssignmentService.AssignAsync</c> performs when the offer
/// is made, not when the provider accepts it. Two consequences worth knowing
/// before changing this: a provider who then rejects sends the booking back to
/// AwaitingFulfilment, and the next assignment fires a second ProviderAssigned
/// naming somebody else; and a *re*assignment while the booking is already
/// Assigned performs no transition at all, so the customer is never told the
/// name changed. Acceptance-time semantics are <c>ProviderAssignmentAcceptedEvent</c>'s
/// to give and would be a separate row, not a tweak here.
/// </para>
/// <para>
/// <b>DURABILITY - read this before relying on any of these notifications.</b>
/// They are at-most-once, best-effort, and not retried. Dispatch is
/// in-process MediatR, published post-commit by
/// <c>DomainEventDispatchInterceptor</c> with no outbox, no queue and no
/// dead-letter (docs/ARCHITECTURE.md, "DOMAIN EVENT DISPATCH AND DELIVERY").
/// This handler then re-reads booking, customer, provider, device tokens and
/// payment/cancellation/refund rows *after* the transaction committed; any of
/// those reads can throw, and the process dying anywhere between the commit
/// and the send loses the notification permanently with nothing left behind to
/// retry from. The transition itself is already durable; the telling of it is
/// not. Task 276 deliberately did not fix this - a durable intent record
/// written in the same transaction as the status change, plus a sweep over
/// unsent records, is the fix that section prescribes for this handler and its
/// three siblings together, and it is a new row's worth of work rather than a
/// side effect of adding triggers.
/// </para>
/// </summary>
public sealed class BookingNotificationTriggerHandler : INotificationHandler<DomainEventNotification<BookingStatusChangedEvent>>
{
    private readonly ICustomerRepository _customerRepository;
    private readonly IBookingRepository _bookingRepository;
    private readonly IPaymentTransactionRepository _paymentRepository;
    private readonly ICancellationRepository _cancellationRepository;
    private readonly IRefundTransactionRepository _refundRepository;
    private readonly IDeviceTokenRepository _deviceTokenRepository;
    private readonly IProviderRepository _providerRepository;
    private readonly INotificationDispatchService _notificationDispatchService;
    private readonly IOptionsMonitor<FulfilmentNotificationOptions> _fulfilmentOptions;
    private readonly ILogger<BookingNotificationTriggerHandler> _logger;

    public BookingNotificationTriggerHandler(
        ICustomerRepository customerRepository,
        IBookingRepository bookingRepository,
        IPaymentTransactionRepository paymentRepository,
        ICancellationRepository cancellationRepository,
        IRefundTransactionRepository refundRepository,
        IDeviceTokenRepository deviceTokenRepository,
        IProviderRepository providerRepository,
        INotificationDispatchService notificationDispatchService,
        IOptionsMonitor<FulfilmentNotificationOptions> fulfilmentOptions,
        ILogger<BookingNotificationTriggerHandler> logger)
    {
        _customerRepository = customerRepository;
        _bookingRepository = bookingRepository;
        _paymentRepository = paymentRepository;
        _cancellationRepository = cancellationRepository;
        _refundRepository = refundRepository;
        _deviceTokenRepository = deviceTokenRepository;
        _providerRepository = providerRepository;
        _notificationDispatchService = notificationDispatchService;
        _fulfilmentOptions = fulfilmentOptions;
        _logger = logger;
    }

    public async Task Handle(DomainEventNotification<BookingStatusChangedEvent> notification, CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;

        // Not every transition has a notification - Initiated/PaymentPending/
        // AwaitingFulfilment/RefundPending are all silent, and deliberately
        // so: none of them is a fact a customer can act on, and
        // AwaitingFulfilment is reached both from Confirmed and from a
        // provider rejecting a job, where "your booking is waiting again" is
        // noise the customer cannot do anything about.
        var eventTypes = domainEvent.ToStatus switch
        {
            BookingStatus.Confirmed => [NotificationEventType.BookingConfirmed, NotificationEventType.PaymentSuccess],
            BookingStatus.PaymentFailed => [NotificationEventType.PaymentFailed],
            BookingStatus.CancelledByCustomer or BookingStatus.CancelledByAdmin => [NotificationEventType.BookingCancelled],
            BookingStatus.Rescheduled => [NotificationEventType.BookingRescheduled],
            BookingStatus.Refunded => [NotificationEventType.RefundProcessed],
            BookingStatus.Expired => [NotificationEventType.BookingExpired],

            // Task 276: the fulfilment half. One event type per transition -
            // unlike Confirmed, none of these doubles up.
            BookingStatus.Assigned => [NotificationEventType.ProviderAssigned],
            BookingStatus.ProviderEnRoute => [NotificationEventType.ProviderEnRoute],
            BookingStatus.ProviderArrived => [NotificationEventType.ProviderArrived],
            BookingStatus.InProgress => [NotificationEventType.JobStarted],
            BookingStatus.Completed => [NotificationEventType.JobCompleted],

            _ => Array.Empty<NotificationEventType>()
        };

        // Task 276: ops mute, applied before any repository read so a muted
        // event costs nothing at all. Only the five fulfilment events can be
        // muted; FulfilmentNotificationOptions.IsEnabled returns true for
        // everything else, so the money-and-cancellation notifications above
        // are unreachable from configuration.
        var options = _fulfilmentOptions.CurrentValue;
        eventTypes = eventTypes.Where(options.IsEnabled).ToArray();

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
        var deviceTokens = await _deviceTokenRepository.ListActiveByCustomerAsync(booking.CustomerId);
        var recipient = new NotificationRecipient(
            customer?.Mobile ?? booking.CustomerMobileSnapshot, customer?.Email, deviceTokens.Select(t => t.Token).ToList());

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

            // Task 276: the fulfilment events are the only ones that name the
            // provider.
            case NotificationEventType.ProviderAssigned:
            case NotificationEventType.ProviderEnRoute:
            case NotificationEventType.ProviderArrived:
            case NotificationEventType.JobStarted:
            case NotificationEventType.JobCompleted:
            {
                await AddProviderVariablesAsync(variables, booking);
                break;
            }
        }

        return variables;
    }

    /// <summary>
    /// Adds the provider identity the fulfilment templates render (task 276).
    ///
    /// <para>
    /// <b>ProviderMobile is masked here, at the only place it enters a
    /// template's variable set</b> - not in the template, and not at the
    /// channel. Template bodies are admin-editable at runtime
    /// (<c>NotificationTemplatesController</c>), so a variable that is unsafe
    /// only until someone types <c>{{ProviderMobile}}</c> into a text box is
    /// unsafe. Masking it before it is offered means no edit to any template,
    /// by anyone, can put a reachable provider number into an SMS. It is
    /// deliberately absent from every default body; the customer-facing place
    /// to reveal a contact is task 275's tracking response, which applies the
    /// same <see cref="ContactMasking"/> rule.
    /// </para>
    ///
    /// <para>
    /// Falls back to a neutral "Your professional" rather than leaving the
    /// placeholder empty: <c>AssignedProviderId</c> is nullable and the
    /// provider row could have been deleted, and "  is on the way" is a worse
    /// message than a slightly generic one. This is a fifth post-commit
    /// repository read - see the class doc comment on durability.
    /// </para>
    /// </summary>
    private async Task AddProviderVariablesAsync(Dictionary<string, string> variables, Booking booking)
    {
        const string UnknownProviderName = "Your professional";

        var provider = booking.AssignedProviderId is { } providerId
            ? await _providerRepository.GetByIdAsync(providerId)
            : null;

        if (provider is null)
        {
            _logger.LogWarning(
                "Booking {BookingId} reached a fulfilment status with no resolvable assigned provider - notifying with a generic name.", booking.Id);
        }

        variables["ProviderName"] = string.IsNullOrWhiteSpace(provider?.DisplayName) ? UnknownProviderName : provider.DisplayName;
        variables["ProviderMobile"] = ContactMasking.MaskOrNull(provider?.Phone) ?? string.Empty;
    }
}

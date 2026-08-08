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
/// <b>Task 276 - the fulfilment half.</b> ProviderEnRoute/ProviderArrived/
/// InProgress/Completed used to map to nothing, so the entire second half of a
/// booking was silent. All four now dispatch from
/// <see cref="BookingStatusChangedEvent"/> rather than from task 272's
/// <c>ProviderEnRouteEvent</c>/<c>ProviderArrivedEvent</c>. Reasons, in order
/// of weight:
/// </para>
/// <list type="number">
/// <item>Every one of the four is a single <see cref="Booking.TransitionTo"/>
/// call, which raises exactly one <see cref="BookingStatusChangedEvent"/> - so
/// "exactly once per transition" is a property of the event stream, not
/// something this handler has to enforce.</item>
/// <item>The tracking events cover only two of the four, so mixing sources
/// would mean two handlers, two sets of repository reads and two places to
/// forget a new state.</item>
/// <item>Booking.TransitionTo raises the tracking event <i>in addition to</i>
/// BookingStatusChangedEvent, never instead of it (see its doc comment).
/// Subscribing to both would send "on the way" twice.</item>
/// </list>
/// <para>
/// <b>Task 295 - who is coming, and when the customer is told.</b> The fifth
/// of task 276's triggers, ProviderAssigned, is the exception to the paragraph
/// above: it does <i>not</i> hang off a status transition, because the
/// transition it used to hang off (AwaitingFulfilment -&gt; Assigned) happens
/// when <c>BookingProviderAssignmentService.AssignAsync</c> makes the *offer*,
/// before the provider has answered. The product rule is now:
/// </para>
/// <list type="bullet">
/// <item><b>Notify on acceptance only.</b> ProviderAssigned fires on
/// <see cref="ProviderAssignmentAcceptedEvent"/> (task 272), so a name is
/// announced only once its owner has committed to the job. A provider who
/// rejects, and the next candidate, and the next, cost the customer nothing;
/// they hear one name, once, and it is the right one. The price is that they
/// wait longer to learn who is coming - accepted deliberately, since a wrong
/// name is worse than a late one.</item>
/// <item><b>A change of professional is never silent</b>, and never re-sends
/// ProviderAssigned - that template reads as a first assignment.
/// <see cref="BookingProviderChangedEvent"/> (raised by
/// <c>BookingProviderAssignment.MarkReassigned</c>, which is reached whether
/// or not the booking's status moves) dispatches the distinct
/// <see cref="NotificationEventType.ProviderChanged"/> template. It sends only
/// when the superseded assignment had been <i>accepted</i>: an offer that was
/// never accepted was never announced, so there is no name in the customer's
/// head to correct.</item>
/// </list>
/// <para>
/// Consequence worth stating, because it is the point of the rule: no sequence
/// of offers, rejections and reassignments can produce two messages naming
/// different professionals as "assigned". The only messages that can name a
/// provider as assigned are acceptances, and between two acceptances a
/// ProviderChanged always sits, explaining the swap.
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
public sealed class BookingNotificationTriggerHandler :
    INotificationHandler<DomainEventNotification<BookingStatusChangedEvent>>,
    INotificationHandler<DomainEventNotification<ProviderAssignmentAcceptedEvent>>,
    INotificationHandler<DomainEventNotification<BookingProviderChangedEvent>>
{
    /// <summary>Stand-in for a provider whose row cannot be resolved - see <see cref="AddProviderVariablesAsync"/>.</summary>
    private const string UnknownProviderName = "Your professional";

    private readonly IBookingRepository _bookingRepository;
    private readonly IPaymentTransactionRepository _paymentRepository;
    private readonly ICancellationRepository _cancellationRepository;
    private readonly IRefundTransactionRepository _refundRepository;
    private readonly IProviderRepository _providerRepository;
    private readonly INotificationDispatchService _notificationDispatchService;
    private readonly IOptionsMonitor<FulfilmentNotificationOptions> _fulfilmentOptions;
    private readonly ILogger<BookingNotificationTriggerHandler> _logger;

    public BookingNotificationTriggerHandler(
        IBookingRepository bookingRepository,
        IPaymentTransactionRepository paymentRepository,
        ICancellationRepository cancellationRepository,
        IRefundTransactionRepository refundRepository,
        IProviderRepository providerRepository,
        INotificationDispatchService notificationDispatchService,
        IOptionsMonitor<FulfilmentNotificationOptions> fulfilmentOptions,
        ILogger<BookingNotificationTriggerHandler> logger)
    {
        _bookingRepository = bookingRepository;
        _paymentRepository = paymentRepository;
        _cancellationRepository = cancellationRepository;
        _refundRepository = refundRepository;
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
            //
            // Assigned is deliberately absent (task 295): reaching it means an
            // offer was made, not that anybody accepted it, and the two are
            // not the same fact. ProviderAssigned is dispatched from
            // ProviderAssignmentAcceptedEvent below instead.
            BookingStatus.ProviderEnRoute => [NotificationEventType.ProviderEnRoute],
            BookingStatus.ProviderArrived => [NotificationEventType.ProviderArrived],
            BookingStatus.InProgress => [NotificationEventType.JobStarted],
            BookingStatus.Completed => [NotificationEventType.JobCompleted],

            _ => Array.Empty<NotificationEventType>()
        };

        // Task 276: ops mute, applied before any repository read so a muted
        // event costs nothing at all. Only the fulfilment events can be muted;
        // FulfilmentNotificationOptions.IsEnabled returns true for everything
        // else, so the money-and-cancellation notifications above are
        // unreachable from configuration.
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

        var recipient = await ResolveCustomerRecipientAsync(booking, cancellationToken);

        foreach (var eventType in eventTypes)
        {
            var variables = await BuildVariablesAsync(eventType, booking, cancellationToken);
            await _notificationDispatchService.DispatchAsync(booking.CustomerId, eventType, recipient, variables, bookingId: booking.Id, cancellationToken: cancellationToken);
        }
    }

    /// <summary>
    /// Task 295: the acceptance-time half of "who is coming". This, not the
    /// AwaitingFulfilment -&gt; Assigned transition, is where the customer is
    /// told a name - see the class doc comment for the rule and its price.
    /// </summary>
    /// <remarks>
    /// Exactly once per accepted job without this handler having to check
    /// anything: <c>BookingProviderAssignment.Accept</c> throws unless the row
    /// is still outstanding, so a second Accept raises no second event. A
    /// booking re-offered after a rejection produces one acceptance event for
    /// whichever provider finally takes it, and none for those who did not.
    /// </remarks>
    public Task Handle(DomainEventNotification<ProviderAssignmentAcceptedEvent> notification, CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;

        return DispatchProviderNotificationAsync(
            domainEvent.BookingId,
            NotificationEventType.ProviderAssigned,
            providerId: domainEvent.ProviderId,
            previousProviderId: null,
            cancellationToken);
    }

    /// <summary>
    /// Task 295: an accepted professional was swapped for another one. The
    /// defect this closes is that the swap moves no booking status - an
    /// already-Assigned booking is still Assigned afterwards - so nothing in
    /// the <see cref="BookingStatusChangedEvent"/> stream ever mentioned it and
    /// the customer would have met a stranger at the door.
    /// </summary>
    /// <remarks>
    /// Silent when the superseded assignment had not been accepted: under the
    /// acceptance-only rule that offer was never announced, so there is no
    /// expectation to correct and "your professional has changed" would name
    /// somebody the customer never heard of. The check lives here rather than
    /// in the domain because it is a rule about what we tell people, not about
    /// what happened.
    /// </remarks>
    public Task Handle(DomainEventNotification<BookingProviderChangedEvent> notification, CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;

        if (!domainEvent.PreviousAssignmentAccepted)
        {
            return Task.CompletedTask;
        }

        return DispatchProviderNotificationAsync(
            domainEvent.BookingId,
            NotificationEventType.ProviderChanged,
            providerId: domainEvent.NewProviderId,
            previousProviderId: domainEvent.PreviousProviderId,
            cancellationToken);
    }

    /// <summary>
    /// The shared body of the two assignment-driven triggers. Same shape as
    /// the status-driven <see cref="Handle(DomainEventNotification{BookingStatusChangedEvent}, CancellationToken)"/>
    /// above - mute first, then booking, then recipient, then dispatch - but
    /// takes the provider from the event rather than from
    /// <see cref="Booking.AssignedProviderId"/>: both events name the provider
    /// they are about, and the denormalized field has already moved on to the
    /// next candidate by the time a ProviderChanged is handled.
    /// </summary>
    private async Task DispatchProviderNotificationAsync(
        Guid bookingId,
        NotificationEventType eventType,
        Guid providerId,
        Guid? previousProviderId,
        CancellationToken cancellationToken)
    {
        if (!_fulfilmentOptions.CurrentValue.IsEnabled(eventType))
        {
            return;
        }

        var booking = await _bookingRepository.GetByIdAsync(bookingId);
        if (booking is null)
        {
            _logger.LogWarning("Booking {BookingId} not found while dispatching a {EventType} notification.", bookingId, eventType);
            return;
        }

        var recipient = await ResolveCustomerRecipientAsync(booking, cancellationToken);

        var variables = BuildBaseVariables(booking);
        await AddProviderVariablesAsync(variables, booking.Id, providerId);

        if (previousProviderId is { } previous)
        {
            var previousProvider = await _providerRepository.GetByIdAsync(previous);
            variables["PreviousProviderName"] = string.IsNullOrWhiteSpace(previousProvider?.DisplayName)
                ? UnknownProviderName
                : previousProvider.DisplayName;
        }

        await _notificationDispatchService.DispatchAsync(
            booking.CustomerId, eventType, recipient, variables, bookingId: booking.Id, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Task 277: the customer/device-token lookups this used to do inline now
    /// live in INotificationDispatchService.ResolveRecipientAsync, which is the
    /// same call the provider side needs. Behaviour is unchanged - the ?? below
    /// reproduces the old <c>customer?.Mobile ?? booking.CustomerMobileSnapshot</c>
    /// exactly, including the case where a customer row exists with a blank
    /// mobile (blank wins, snapshot is only a fallback for a missing customer).
    /// The snapshot fallback stays here rather than moving into the resolver
    /// because it is booking data; the resolver only knows principals.
    /// </summary>
    private async Task<NotificationRecipient> ResolveCustomerRecipientAsync(Booking booking, CancellationToken cancellationToken)
    {
        var recipient = await _notificationDispatchService.ResolveRecipientAsync(
            DeviceTokenOwner.ForCustomer(booking.CustomerId), cancellationToken);

        return recipient with { Mobile = recipient.Mobile ?? booking.CustomerMobileSnapshot };
    }

    /// <summary>The variables every booking template can rely on, whichever event brought it here.</summary>
    private static Dictionary<string, string> BuildBaseVariables(Booking booking) => new()
    {
        ["CustomerName"] = booking.CustomerNameSnapshot,
        ["BookingId"] = booking.Id.ToString(),
        ["ServiceName"] = booking.Items.Count > 0 ? booking.Items[0].NameSnapshot : string.Empty,
        ["SlotDate"] = booking.SlotDate.ToString("yyyy-MM-dd"),
        ["SlotWindow"] = booking.SlotWindowNameSnapshot,
        ["TotalPayable"] = booking.TotalPayableSnapshot.ToString("0.00")
    };

    private async Task<Dictionary<string, string>> BuildVariablesAsync(NotificationEventType eventType, Booking booking, CancellationToken cancellationToken)
    {
        var variables = BuildBaseVariables(booking);

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
            // provider. ProviderAssigned and ProviderChanged are the two that
            // never arrive here - they come from the assignment events, which
            // carry the provider they are about (see
            // DispatchProviderNotificationAsync).
            case NotificationEventType.ProviderEnRoute:
            case NotificationEventType.ProviderArrived:
            case NotificationEventType.JobStarted:
            case NotificationEventType.JobCompleted:
            {
                await AddProviderVariablesAsync(variables, booking.Id, booking.AssignedProviderId);
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
    /// <param name="providerId">
    /// The provider this particular message is about. The status-driven
    /// triggers pass <see cref="Booking.AssignedProviderId"/>; the two
    /// assignment-driven ones (task 295) pass the id off their own event,
    /// which is authoritative for them - a ProviderChanged is handled after
    /// the denormalized field has already moved to the incoming provider, and
    /// an acceptance names the accepting provider rather than whoever the
    /// field happens to hold.
    /// </param>
    private async Task AddProviderVariablesAsync(Dictionary<string, string> variables, Guid bookingId, Guid? providerId)
    {
        var provider = providerId is { } id
            ? await _providerRepository.GetByIdAsync(id)
            : null;

        if (provider is null)
        {
            _logger.LogWarning(
                "Booking {BookingId} reached a fulfilment status with no resolvable assigned provider - notifying with a generic name.", bookingId);
        }

        variables["ProviderName"] = string.IsNullOrWhiteSpace(provider?.DisplayName) ? UnknownProviderName : provider.DisplayName;
        variables["ProviderMobile"] = ContactMasking.MaskOrNull(provider?.Phone) ?? string.Empty;
    }
}

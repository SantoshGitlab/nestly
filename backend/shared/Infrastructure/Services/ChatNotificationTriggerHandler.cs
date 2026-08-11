using MediatR;
using Microsoft.Extensions.Logging;
using Nestly.Application;
using Nestly.Application.Bookings;
using Nestly.Application.Chat;
using Nestly.Application.Notifications;
using Nestly.Application.Support;
using Nestly.BuildingBlocks.Primitives;
using Nestly.Domain;
using Nestly.Domain.Events;
using Nestly.Infrastructure.Persistence.Interceptors;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// Offline push/SMS fallback for chat (PRODUCT-ENHANCEMENTS.md IN-APP CHAT
/// "Offline delivery", task 194): a message to a recipient with no live
/// SignalR connection falls back to the existing notification dispatch
/// (task 156, <see cref="INotificationDispatchService"/>) - same calling
/// convention as <c>SupportTicketNotificationTriggerHandler</c>.
///
/// Scope gap, documented rather than silently assumed: this only resolves
/// and notifies the *customer* side of a thread. When the customer is the
/// sender, the recipient is either the admin support team (context_type
/// support_ticket) or the assigned provider (context_type booking) - neither
/// has a single-identity, device-token-backed notification path wired up
/// yet (admins already watch the live support console per task 193; provider
/// push would need provider-api's own device-token infrastructure, which
/// task 193's provider reply view does not yet exist to produce messages
/// from anyway). Revisit once provider-side chat lands.
///
/// <para>
/// <b>Task 294 - durable, and the presence check is why the Skipped state
/// exists.</b> The intent to notify is committed with the chat message
/// itself, before anything is known about whether the recipient is online.
/// Presence is a live fact that can only be read at delivery time, and it can
/// legitimately answer "do not send" - so a recipient found online resolves
/// the intent to <see cref="NotificationIntentStatus.Skipped"/> rather than
/// leaving it pending for the sweep to keep re-offering. Note the consequence,
/// which is the right one: if this handler dies before the presence check, the
/// sweep re-runs it minutes later and re-reads presence <i>then</i> - a
/// customer who has since come online is correctly not pushed to.
/// </para>
/// </summary>
public sealed class ChatNotificationTriggerHandler :
    INotificationHandler<DomainEventNotification<ChatMessageSentEvent>>,
    INotificationTriggerHandler
{
    private readonly IBookingRepository _bookingRepository;
    private readonly ISupportTicketRepository _supportTicketRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IDeviceTokenRepository _deviceTokenRepository;
    private readonly IChatPresenceTracker _presenceTracker;
    private readonly INotificationDispatchService _notificationDispatchService;
    private readonly INotificationIntentCoordinator _intentCoordinator;
    private readonly ILogger<ChatNotificationTriggerHandler> _logger;

    public ChatNotificationTriggerHandler(
        IBookingRepository bookingRepository,
        ISupportTicketRepository supportTicketRepository,
        ICustomerRepository customerRepository,
        IDeviceTokenRepository deviceTokenRepository,
        IChatPresenceTracker presenceTracker,
        INotificationDispatchService notificationDispatchService,
        INotificationIntentCoordinator intentCoordinator,
        ILogger<ChatNotificationTriggerHandler> logger)
    {
        _bookingRepository = bookingRepository;
        _supportTicketRepository = supportTicketRepository;
        _customerRepository = customerRepository;
        _deviceTokenRepository = deviceTokenRepository;
        _presenceTracker = presenceTracker;
        _notificationDispatchService = notificationDispatchService;
        _intentCoordinator = intentCoordinator;
        _logger = logger;
    }

    public bool CanHandle(Type domainEventType) => domainEventType == typeof(ChatMessageSentEvent);

    public Task HandleAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default) =>
        domainEvent is ChatMessageSentEvent messageSent
            ? HandleAsync(messageSent, cancellationToken)
            : Task.CompletedTask;

    public Task Handle(DomainEventNotification<ChatMessageSentEvent> notification, CancellationToken cancellationToken) =>
        HandleAsync(notification.DomainEvent, cancellationToken);

    private async Task HandleAsync(ChatMessageSentEvent domainEvent, CancellationToken cancellationToken)
    {
        if (domainEvent.SenderType == ChatSenderType.Customer)
        {
            // The customer is the sender, not the recipient - see this
            // handler's doc comment for why the other direction isn't
            // notified yet. NotificationIntentPlanner agrees, so there is no
            // intent row to resolve here.
            return;
        }

        Guid? customerId = domainEvent.ContextType switch
        {
            ChatContextType.Booking => (await _bookingRepository.GetByIdAsync(domainEvent.ContextId))?.CustomerId,
            ChatContextType.SupportTicket => (await _supportTicketRepository.GetByIdAsync(domainEvent.ContextId))?.CustomerId,
            _ => null
        };

        if (customerId is null)
        {
            _logger.LogWarning(
                "Chat message {MessageId} could not resolve a recipient customer for {ContextType} {ContextId}.",
                domainEvent.MessageId, domainEvent.ContextType, domainEvent.ContextId);
            await _intentCoordinator.SkipAsync(
                domainEvent, NotificationEventType.NewChatMessage, "No recipient customer could be resolved for the thread.", cancellationToken);
            return;
        }

        // Fail-open by design (see IChatPresenceTracker): an unknown or
        // errored presence read is treated as offline, so the notification
        // still fires rather than being silently skipped.
        if (await _presenceTracker.IsOnlineAsync(customerId.Value, cancellationToken))
        {
            await _intentCoordinator.SkipAsync(
                domainEvent, NotificationEventType.NewChatMessage, "Recipient has a live connection.", cancellationToken);
            return;
        }

        var customer = await _customerRepository.GetByIdAsync(customerId.Value);
        if (customer is null)
        {
            _logger.LogWarning("Customer {CustomerId} not found while dispatching a chat-message notification.", customerId);
            await _intentCoordinator.SkipAsync(
                domainEvent, NotificationEventType.NewChatMessage, "Customer no longer exists.", cancellationToken);
            return;
        }

        await _intentCoordinator.DeliverAsync(
            domainEvent,
            NotificationEventType.NewChatMessage,
            async ct =>
            {
                var variables = new Dictionary<string, string>
                {
                    ["CustomerName"] = customer.Name,
                    ["SenderType"] = domainEvent.SenderType.ToString(),
                    ["MessagePreview"] = domainEvent.Body.Length > 120 ? domainEvent.Body[..120] + "..." : domainEvent.Body
                };

                var deviceTokens = await _deviceTokenRepository.ListActiveByOwnerAsync(DeviceTokenOwner.ForCustomer(customerId.Value));

                await _notificationDispatchService.DispatchAsync(
                    customerId.Value,
                    NotificationEventType.NewChatMessage,
                    new NotificationRecipient(customer.Mobile, customer.Email, deviceTokens.Select(t => t.Token).ToList()),
                    variables,
                    bookingId: domainEvent.ContextType == ChatContextType.Booking ? domainEvent.ContextId : null,
                    supportTicketId: domainEvent.ContextType == ChatContextType.SupportTicket ? domainEvent.ContextId : null,
                    cancellationToken: ct);
            },
            cancellationToken);
    }
}

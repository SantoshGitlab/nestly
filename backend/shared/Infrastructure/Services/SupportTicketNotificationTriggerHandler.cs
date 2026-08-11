using MediatR;
using Microsoft.Extensions.Logging;
using Nestly.Application;
using Nestly.Application.Notifications;
using Nestly.Application.Support;
using Nestly.BuildingBlocks.Primitives;
using Nestly.Domain;
using Nestly.Domain.Events;
using Nestly.Infrastructure.Persistence.Interceptors;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// Notification trigger wiring for support ticket status changes (SRS 19.1,
/// task 88g).
///
/// <para>
/// <b>Task 294 - durable.</b> The dispatch below is wrapped in
/// <see cref="INotificationIntentCoordinator"/>, so the intent to send it was
/// committed alongside the status change itself and the sweep will deliver it
/// if this post-commit path dies. Being reachable through
/// <see cref="INotificationTriggerHandler"/> is what lets the sweep re-run
/// exactly this code rather than a copy of it.
/// </para>
/// </summary>
public sealed class SupportTicketNotificationTriggerHandler :
    INotificationHandler<DomainEventNotification<SupportTicketStatusChangedEvent>>,
    INotificationTriggerHandler
{
    private readonly ICustomerRepository _customerRepository;
    private readonly ISupportTicketRepository _ticketRepository;
    private readonly IDeviceTokenRepository _deviceTokenRepository;
    private readonly INotificationDispatchService _notificationDispatchService;
    private readonly INotificationIntentCoordinator _intentCoordinator;
    private readonly ILogger<SupportTicketNotificationTriggerHandler> _logger;

    public SupportTicketNotificationTriggerHandler(
        ICustomerRepository customerRepository,
        ISupportTicketRepository ticketRepository,
        IDeviceTokenRepository deviceTokenRepository,
        INotificationDispatchService notificationDispatchService,
        INotificationIntentCoordinator intentCoordinator,
        ILogger<SupportTicketNotificationTriggerHandler> logger)
    {
        _customerRepository = customerRepository;
        _ticketRepository = ticketRepository;
        _deviceTokenRepository = deviceTokenRepository;
        _notificationDispatchService = notificationDispatchService;
        _intentCoordinator = intentCoordinator;
        _logger = logger;
    }

    public bool CanHandle(Type domainEventType) => domainEventType == typeof(SupportTicketStatusChangedEvent);

    public Task HandleAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default) =>
        domainEvent is SupportTicketStatusChangedEvent statusChanged
            ? HandleAsync(statusChanged, cancellationToken)
            : Task.CompletedTask;

    public Task Handle(DomainEventNotification<SupportTicketStatusChangedEvent> notification, CancellationToken cancellationToken) =>
        HandleAsync(notification.DomainEvent, cancellationToken);

    private async Task HandleAsync(SupportTicketStatusChangedEvent domainEvent, CancellationToken cancellationToken)
    {
        var ticket = await _ticketRepository.GetByIdAsync(domainEvent.TicketId);
        if (ticket is null)
        {
            _logger.LogWarning("Support ticket {TicketId} not found while dispatching a status-update notification.", domainEvent.TicketId);
            await _intentCoordinator.SkipAsync(
                domainEvent, NotificationEventType.SupportTicketUpdate, "Support ticket no longer exists.", cancellationToken);
            return;
        }

        var customer = await _customerRepository.GetByIdAsync(domainEvent.CustomerId);
        if (customer is null)
        {
            _logger.LogWarning("Customer {CustomerId} not found while dispatching a ticket-update notification.", domainEvent.CustomerId);
            await _intentCoordinator.SkipAsync(
                domainEvent, NotificationEventType.SupportTicketUpdate, "Customer no longer exists.", cancellationToken);
            return;
        }

        await _intentCoordinator.DeliverAsync(
            domainEvent,
            NotificationEventType.SupportTicketUpdate,
            async ct =>
            {
                var variables = new Dictionary<string, string>
                {
                    ["CustomerName"] = customer.Name,
                    ["TicketId"] = ticket.Id.ToString(),
                    ["Subject"] = ticket.Subject,
                    ["Status"] = domainEvent.ToStatus.ToString()
                };

                var deviceTokens = await _deviceTokenRepository.ListActiveByOwnerAsync(DeviceTokenOwner.ForCustomer(domainEvent.CustomerId));

                await _notificationDispatchService.DispatchAsync(
                    domainEvent.CustomerId,
                    NotificationEventType.SupportTicketUpdate,
                    new NotificationRecipient(customer.Mobile, customer.Email, deviceTokens.Select(t => t.Token).ToList()),
                    variables,
                    bookingId: ticket.BookingId,
                    supportTicketId: ticket.Id,
                    cancellationToken: ct);
            },
            cancellationToken);
    }
}

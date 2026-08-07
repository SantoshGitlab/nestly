using MediatR;
using Microsoft.Extensions.Logging;
using Nestly.Application;
using Nestly.Application.Notifications;
using Nestly.Application.Support;
using Nestly.Domain;
using Nestly.Domain.Events;
using Nestly.Infrastructure.Persistence.Interceptors;

namespace Nestly.Infrastructure.Services;

/// <summary>Notification trigger wiring for support ticket status changes (SRS 19.1, task 88g).</summary>
public sealed class SupportTicketNotificationTriggerHandler : INotificationHandler<DomainEventNotification<SupportTicketStatusChangedEvent>>
{
    private readonly ICustomerRepository _customerRepository;
    private readonly ISupportTicketRepository _ticketRepository;
    private readonly IDeviceTokenRepository _deviceTokenRepository;
    private readonly INotificationDispatchService _notificationDispatchService;
    private readonly ILogger<SupportTicketNotificationTriggerHandler> _logger;

    public SupportTicketNotificationTriggerHandler(
        ICustomerRepository customerRepository,
        ISupportTicketRepository ticketRepository,
        IDeviceTokenRepository deviceTokenRepository,
        INotificationDispatchService notificationDispatchService,
        ILogger<SupportTicketNotificationTriggerHandler> logger)
    {
        _customerRepository = customerRepository;
        _ticketRepository = ticketRepository;
        _deviceTokenRepository = deviceTokenRepository;
        _notificationDispatchService = notificationDispatchService;
        _logger = logger;
    }

    public async Task Handle(DomainEventNotification<SupportTicketStatusChangedEvent> notification, CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;

        var ticket = await _ticketRepository.GetByIdAsync(domainEvent.TicketId);
        if (ticket is null)
        {
            _logger.LogWarning("Support ticket {TicketId} not found while dispatching a status-update notification.", domainEvent.TicketId);
            return;
        }

        var customer = await _customerRepository.GetByIdAsync(domainEvent.CustomerId);
        if (customer is null)
        {
            _logger.LogWarning("Customer {CustomerId} not found while dispatching a ticket-update notification.", domainEvent.CustomerId);
            return;
        }

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
            cancellationToken: cancellationToken);
    }
}

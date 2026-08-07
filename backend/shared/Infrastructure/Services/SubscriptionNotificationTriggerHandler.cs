using MediatR;
using Microsoft.Extensions.Logging;
using Nestly.Application;
using Nestly.Application.Notifications;
using Nestly.Domain;
using Nestly.Domain.Events;
using Nestly.Infrastructure.Persistence.Interceptors;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// Task 183: dispatches renewed / expiring-soon / payment-failed
/// notifications for the 3 domain events <see cref="SubscriptionBillingJob"/>
/// raises via <see cref="Domain.CustomerSubscription"/>'s domain methods -
/// same shape as <c>ChatNotificationTriggerHandler</c>/
/// <c>ReferralQualifyingBookingHandler</c>, one handler class per
/// independent concern reacting to an event on its own.
/// </summary>
public sealed class SubscriptionNotificationTriggerHandler :
    INotificationHandler<DomainEventNotification<SubscriptionRenewedEvent>>,
    INotificationHandler<DomainEventNotification<SubscriptionExpiringSoonEvent>>,
    INotificationHandler<DomainEventNotification<SubscriptionPaymentFailedEvent>>
{
    private readonly ICustomerRepository _customerRepository;
    private readonly IDeviceTokenRepository _deviceTokenRepository;
    private readonly INotificationDispatchService _notificationDispatchService;
    private readonly ILogger<SubscriptionNotificationTriggerHandler> _logger;

    public SubscriptionNotificationTriggerHandler(
        ICustomerRepository customerRepository,
        IDeviceTokenRepository deviceTokenRepository,
        INotificationDispatchService notificationDispatchService,
        ILogger<SubscriptionNotificationTriggerHandler> logger)
    {
        _customerRepository = customerRepository;
        _deviceTokenRepository = deviceTokenRepository;
        _notificationDispatchService = notificationDispatchService;
        _logger = logger;
    }

    public Task Handle(DomainEventNotification<SubscriptionRenewedEvent> notification, CancellationToken cancellationToken) =>
        DispatchAsync(notification.DomainEvent.CustomerId, NotificationEventType.SubscriptionRenewed, variables: null, cancellationToken);

    public Task Handle(DomainEventNotification<SubscriptionExpiringSoonEvent> notification, CancellationToken cancellationToken) =>
        DispatchAsync(notification.DomainEvent.CustomerId, NotificationEventType.SubscriptionExpiringSoon, variables: null, cancellationToken);

    public Task Handle(DomainEventNotification<SubscriptionPaymentFailedEvent> notification, CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;
        var variables = new Dictionary<string, string> { ["IsFinal"] = domainEvent.IsFinal ? "true" : "false" };
        return DispatchAsync(domainEvent.CustomerId, NotificationEventType.SubscriptionPaymentFailed, variables, cancellationToken);
    }

    private async Task DispatchAsync(
        Guid customerId, NotificationEventType eventType, Dictionary<string, string>? variables, CancellationToken cancellationToken)
    {
        var customer = await _customerRepository.GetByIdAsync(customerId);
        if (customer is null)
        {
            _logger.LogWarning("Subscription notification {EventType} found no customer for id {CustomerId}.", eventType, customerId);
            return;
        }

        var deviceTokens = await _deviceTokenRepository.ListActiveByOwnerAsync(DeviceTokenOwner.ForCustomer(customerId));
        var recipient = new NotificationRecipient(customer.Mobile, customer.Email, deviceTokens.Select(t => t.Token).ToList());

        await _notificationDispatchService.DispatchAsync(
            customerId, eventType, recipient, variables ?? new Dictionary<string, string>(), cancellationToken: cancellationToken);
    }
}

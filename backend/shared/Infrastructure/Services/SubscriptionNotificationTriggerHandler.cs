using MediatR;
using Microsoft.Extensions.Logging;
using Nestly.Application;
using Nestly.Application.Notifications;
using Nestly.BuildingBlocks.Primitives;
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
///
/// <para>
/// <b>Task 294 - durable.</b> Every dispatch goes through
/// <see cref="INotificationIntentCoordinator"/>, so the intent commits with
/// the billing state change that warranted it and the sweep re-runs this
/// handler if the post-commit path never got there. That matters more here
/// than almost anywhere else: these three fire from a background billing job
/// whose failure nobody is watching a screen for, and "your payment failed" is
/// the one message a customer has to act on.
/// </para>
/// </summary>
public sealed class SubscriptionNotificationTriggerHandler :
    INotificationHandler<DomainEventNotification<SubscriptionRenewedEvent>>,
    INotificationHandler<DomainEventNotification<SubscriptionExpiringSoonEvent>>,
    INotificationHandler<DomainEventNotification<SubscriptionPaymentFailedEvent>>,
    INotificationTriggerHandler
{
    private readonly ICustomerRepository _customerRepository;
    private readonly IDeviceTokenRepository _deviceTokenRepository;
    private readonly INotificationDispatchService _notificationDispatchService;
    private readonly INotificationIntentCoordinator _intentCoordinator;
    private readonly ILogger<SubscriptionNotificationTriggerHandler> _logger;

    public SubscriptionNotificationTriggerHandler(
        ICustomerRepository customerRepository,
        IDeviceTokenRepository deviceTokenRepository,
        INotificationDispatchService notificationDispatchService,
        INotificationIntentCoordinator intentCoordinator,
        ILogger<SubscriptionNotificationTriggerHandler> logger)
    {
        _customerRepository = customerRepository;
        _deviceTokenRepository = deviceTokenRepository;
        _notificationDispatchService = notificationDispatchService;
        _intentCoordinator = intentCoordinator;
        _logger = logger;
    }

    public bool CanHandle(Type domainEventType) =>
        domainEventType == typeof(SubscriptionRenewedEvent) ||
        domainEventType == typeof(SubscriptionExpiringSoonEvent) ||
        domainEventType == typeof(SubscriptionPaymentFailedEvent);

    public Task HandleAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default) => domainEvent switch
    {
        SubscriptionRenewedEvent renewed => HandleAsync(renewed, cancellationToken),
        SubscriptionExpiringSoonEvent expiring => HandleAsync(expiring, cancellationToken),
        SubscriptionPaymentFailedEvent paymentFailed => HandleAsync(paymentFailed, cancellationToken),
        _ => Task.CompletedTask
    };

    public Task Handle(DomainEventNotification<SubscriptionRenewedEvent> notification, CancellationToken cancellationToken) =>
        HandleAsync(notification.DomainEvent, cancellationToken);

    public Task Handle(DomainEventNotification<SubscriptionExpiringSoonEvent> notification, CancellationToken cancellationToken) =>
        HandleAsync(notification.DomainEvent, cancellationToken);

    public Task Handle(DomainEventNotification<SubscriptionPaymentFailedEvent> notification, CancellationToken cancellationToken) =>
        HandleAsync(notification.DomainEvent, cancellationToken);

    private Task HandleAsync(SubscriptionRenewedEvent domainEvent, CancellationToken cancellationToken) =>
        DispatchAsync(domainEvent, domainEvent.CustomerId, NotificationEventType.SubscriptionRenewed, variables: null, cancellationToken);

    private Task HandleAsync(SubscriptionExpiringSoonEvent domainEvent, CancellationToken cancellationToken) =>
        DispatchAsync(domainEvent, domainEvent.CustomerId, NotificationEventType.SubscriptionExpiringSoon, variables: null, cancellationToken);

    private Task HandleAsync(SubscriptionPaymentFailedEvent domainEvent, CancellationToken cancellationToken)
    {
        var variables = new Dictionary<string, string> { ["IsFinal"] = domainEvent.IsFinal ? "true" : "false" };
        return DispatchAsync(domainEvent, domainEvent.CustomerId, NotificationEventType.SubscriptionPaymentFailed, variables, cancellationToken);
    }

    private async Task DispatchAsync(
        IDomainEvent domainEvent,
        Guid customerId,
        NotificationEventType eventType,
        Dictionary<string, string>? variables,
        CancellationToken cancellationToken)
    {
        var customer = await _customerRepository.GetByIdAsync(customerId);
        if (customer is null)
        {
            _logger.LogWarning("Subscription notification {EventType} found no customer for id {CustomerId}.", eventType, customerId);
            await _intentCoordinator.SkipAsync(domainEvent, eventType, "Customer no longer exists.", cancellationToken);
            return;
        }

        await _intentCoordinator.DeliverAsync(
            domainEvent,
            eventType,
            async ct =>
            {
                var deviceTokens = await _deviceTokenRepository.ListActiveByOwnerAsync(DeviceTokenOwner.ForCustomer(customerId));
                var recipient = new NotificationRecipient(customer.Mobile, customer.Email, deviceTokens.Select(t => t.Token).ToList());

                await _notificationDispatchService.DispatchAsync(
                    customerId, eventType, recipient, variables ?? new Dictionary<string, string>(), cancellationToken: ct);
            },
            cancellationToken);
    }
}

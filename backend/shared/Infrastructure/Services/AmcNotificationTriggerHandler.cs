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
/// Phase 20 AMC module (docs/AMC.md): dispatches purchased / visit-redeemed /
/// expiring-soon / exhausted notifications for the 4 domain events
/// <see cref="CustomerAmcContract"/> raises - same shape and same
/// per-event-type durability guarantee as <see cref="SubscriptionNotificationTriggerHandler"/>,
/// whose doc comment explains why every dispatch goes through
/// <see cref="INotificationIntentCoordinator"/> rather than sending directly.
/// </summary>
public sealed class AmcNotificationTriggerHandler :
    INotificationHandler<DomainEventNotification<AmcContractPurchasedEvent>>,
    INotificationHandler<DomainEventNotification<AmcVisitRedeemedEvent>>,
    INotificationHandler<DomainEventNotification<AmcContractExpiringSoonEvent>>,
    INotificationHandler<DomainEventNotification<AmcContractExhaustedEvent>>,
    INotificationTriggerHandler
{
    private readonly ICustomerRepository _customerRepository;
    private readonly IDeviceTokenRepository _deviceTokenRepository;
    private readonly INotificationDispatchService _notificationDispatchService;
    private readonly INotificationIntentCoordinator _intentCoordinator;
    private readonly ILogger<AmcNotificationTriggerHandler> _logger;

    public AmcNotificationTriggerHandler(
        ICustomerRepository customerRepository,
        IDeviceTokenRepository deviceTokenRepository,
        INotificationDispatchService notificationDispatchService,
        INotificationIntentCoordinator intentCoordinator,
        ILogger<AmcNotificationTriggerHandler> logger)
    {
        _customerRepository = customerRepository;
        _deviceTokenRepository = deviceTokenRepository;
        _notificationDispatchService = notificationDispatchService;
        _intentCoordinator = intentCoordinator;
        _logger = logger;
    }

    public bool CanHandle(Type domainEventType) =>
        domainEventType == typeof(AmcContractPurchasedEvent) ||
        domainEventType == typeof(AmcVisitRedeemedEvent) ||
        domainEventType == typeof(AmcContractExpiringSoonEvent) ||
        domainEventType == typeof(AmcContractExhaustedEvent);

    public Task HandleAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default) => domainEvent switch
    {
        AmcContractPurchasedEvent purchased => HandleAsync(purchased, cancellationToken),
        AmcVisitRedeemedEvent redeemed => HandleAsync(redeemed, cancellationToken),
        AmcContractExpiringSoonEvent expiring => HandleAsync(expiring, cancellationToken),
        AmcContractExhaustedEvent exhausted => HandleAsync(exhausted, cancellationToken),
        _ => Task.CompletedTask
    };

    public Task Handle(DomainEventNotification<AmcContractPurchasedEvent> notification, CancellationToken cancellationToken) =>
        HandleAsync(notification.DomainEvent, cancellationToken);

    public Task Handle(DomainEventNotification<AmcVisitRedeemedEvent> notification, CancellationToken cancellationToken) =>
        HandleAsync(notification.DomainEvent, cancellationToken);

    public Task Handle(DomainEventNotification<AmcContractExpiringSoonEvent> notification, CancellationToken cancellationToken) =>
        HandleAsync(notification.DomainEvent, cancellationToken);

    public Task Handle(DomainEventNotification<AmcContractExhaustedEvent> notification, CancellationToken cancellationToken) =>
        HandleAsync(notification.DomainEvent, cancellationToken);

    private Task HandleAsync(AmcContractPurchasedEvent domainEvent, CancellationToken cancellationToken) =>
        DispatchAsync(domainEvent, domainEvent.CustomerId, NotificationEventType.AmcContractPurchased, variables: null, cancellationToken);

    private Task HandleAsync(AmcVisitRedeemedEvent domainEvent, CancellationToken cancellationToken)
    {
        var variables = new Dictionary<string, string> { ["VisitsRemaining"] = domainEvent.VisitsRemaining.ToString() };
        return DispatchAsync(domainEvent, domainEvent.CustomerId, NotificationEventType.AmcVisitRedeemed, variables, cancellationToken);
    }

    private Task HandleAsync(AmcContractExpiringSoonEvent domainEvent, CancellationToken cancellationToken) =>
        DispatchAsync(domainEvent, domainEvent.CustomerId, NotificationEventType.AmcContractExpiringSoon, variables: null, cancellationToken);

    private Task HandleAsync(AmcContractExhaustedEvent domainEvent, CancellationToken cancellationToken) =>
        DispatchAsync(domainEvent, domainEvent.CustomerId, NotificationEventType.AmcContractExhausted, variables: null, cancellationToken);

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
            _logger.LogWarning("AMC notification {EventType} found no customer for id {CustomerId}.", eventType, customerId);
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

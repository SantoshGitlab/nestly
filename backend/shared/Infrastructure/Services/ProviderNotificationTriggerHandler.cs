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
/// Notification trigger wiring for provider onboarding events - KYC document
/// review and go-live activation (task 88h). Until this handler, an admin
/// approving/rejecting a KYC document or activating a provider
/// (<see cref="ProviderKycApprovalService"/>) raised no notification at all;
/// the provider only found out by reopening the app.
///
/// <para>
/// Same durable-dispatch shape as the other trigger handlers (task 294): the
/// send below runs through <see cref="INotificationIntentCoordinator"/>, so
/// the sweep can re-run exactly this code if the post-commit path dies before
/// it completes.
/// </para>
/// </summary>
public sealed class ProviderNotificationTriggerHandler :
    INotificationHandler<DomainEventNotification<ProviderKycDocumentApprovedEvent>>,
    INotificationHandler<DomainEventNotification<ProviderKycDocumentRejectedEvent>>,
    INotificationHandler<DomainEventNotification<ProviderActivatedEvent>>,
    INotificationTriggerHandler
{
    private readonly IProviderRepository _providerRepository;
    private readonly INotificationDispatchService _notificationDispatchService;
    private readonly INotificationIntentCoordinator _intentCoordinator;
    private readonly ILogger<ProviderNotificationTriggerHandler> _logger;

    public ProviderNotificationTriggerHandler(
        IProviderRepository providerRepository,
        INotificationDispatchService notificationDispatchService,
        INotificationIntentCoordinator intentCoordinator,
        ILogger<ProviderNotificationTriggerHandler> logger)
    {
        _providerRepository = providerRepository;
        _notificationDispatchService = notificationDispatchService;
        _intentCoordinator = intentCoordinator;
        _logger = logger;
    }

    public bool CanHandle(Type domainEventType) =>
        domainEventType == typeof(ProviderKycDocumentApprovedEvent) ||
        domainEventType == typeof(ProviderKycDocumentRejectedEvent) ||
        domainEventType == typeof(ProviderActivatedEvent);

    public Task HandleAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default) => domainEvent switch
    {
        ProviderKycDocumentApprovedEvent approved => HandleDocumentReviewedAsync(approved.ProviderId, approved, NotificationEventType.ProviderKycApproved, approved.DocType, cancellationToken),
        ProviderKycDocumentRejectedEvent rejected => HandleDocumentReviewedAsync(rejected.ProviderId, rejected, NotificationEventType.ProviderKycRejected, rejected.DocType, cancellationToken),
        ProviderActivatedEvent activated => HandleActivatedAsync(activated, cancellationToken),
        _ => Task.CompletedTask
    };

    public Task Handle(DomainEventNotification<ProviderKycDocumentApprovedEvent> notification, CancellationToken cancellationToken) =>
        HandleAsync(notification.DomainEvent, cancellationToken);

    public Task Handle(DomainEventNotification<ProviderKycDocumentRejectedEvent> notification, CancellationToken cancellationToken) =>
        HandleAsync(notification.DomainEvent, cancellationToken);

    public Task Handle(DomainEventNotification<ProviderActivatedEvent> notification, CancellationToken cancellationToken) =>
        HandleAsync(notification.DomainEvent, cancellationToken);

    private async Task HandleDocumentReviewedAsync(
        Guid providerId, IDomainEvent domainEvent, NotificationEventType eventType, ProviderKycDocumentType docType, CancellationToken cancellationToken)
    {
        var provider = await _providerRepository.GetByIdAsync(providerId);
        if (provider is null)
        {
            _logger.LogWarning("Provider {ProviderId} not found while dispatching a {EventType} notification.", providerId, eventType);
            await _intentCoordinator.SkipAsync(domainEvent, eventType, "Provider no longer exists.", cancellationToken);
            return;
        }

        var recipient = await _notificationDispatchService.ResolveRecipientAsync(DeviceTokenOwner.ForProvider(providerId), cancellationToken);

        await _intentCoordinator.DeliverAsync(
            domainEvent,
            eventType,
            async ct =>
            {
                var variables = new Dictionary<string, string>
                {
                    ["ProviderName"] = provider.DisplayName,
                    ["DocType"] = docType.ToString()
                };

                await _notificationDispatchService.DispatchAsync(
                    customerId: null, eventType, recipient, variables, providerId: providerId, cancellationToken: ct);
            },
            cancellationToken);
    }

    private async Task HandleActivatedAsync(ProviderActivatedEvent domainEvent, CancellationToken cancellationToken)
    {
        var provider = await _providerRepository.GetByIdAsync(domainEvent.ProviderId);
        if (provider is null)
        {
            _logger.LogWarning("Provider {ProviderId} not found while dispatching a {EventType} notification.", domainEvent.ProviderId, NotificationEventType.ProviderActivated);
            await _intentCoordinator.SkipAsync(domainEvent, NotificationEventType.ProviderActivated, "Provider no longer exists.", cancellationToken);
            return;
        }

        var recipient = await _notificationDispatchService.ResolveRecipientAsync(DeviceTokenOwner.ForProvider(domainEvent.ProviderId), cancellationToken);

        await _intentCoordinator.DeliverAsync(
            domainEvent,
            NotificationEventType.ProviderActivated,
            async ct =>
            {
                var variables = new Dictionary<string, string> { ["ProviderName"] = provider.DisplayName };

                await _notificationDispatchService.DispatchAsync(
                    customerId: null, NotificationEventType.ProviderActivated, recipient, variables, providerId: domainEvent.ProviderId, cancellationToken: ct);
            },
            cancellationToken);
    }
}

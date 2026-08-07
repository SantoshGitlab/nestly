using System.Text.Json;
using Microsoft.Extensions.Logging;
using Nestly.Application;
using Nestly.Application.Abstractions.Observability;
using Nestly.Application.Notifications;
using Nestly.BuildingBlocks.Privacy;
using Nestly.Domain;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// Renders and sends a notification across every channel the recipient has
/// contact details for (SRS 19.1-2, tasks 87c-d). Each channel attempt is
/// logged as its own <see cref="NotificationEvent"/> - created Pending
/// before the send, then updated to Sent/Failed after - so a crash mid-send
/// leaves an honest Pending row rather than no record at all.
/// </summary>
public class NotificationDispatchService : INotificationDispatchService
{
    private readonly INotificationTemplateRenderer _templateRenderer;
    private readonly INotificationProvider _notificationProvider;
    private readonly IPushNotificationProvider _pushNotificationProvider;
    private readonly INotificationEventRepository _repository;
    private readonly IDeviceTokenRepository _deviceTokenRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IProviderRepository _providerRepository;
    private readonly IMetricsService _metricsService;
    private readonly ILogger<NotificationDispatchService> _logger;

    public NotificationDispatchService(
        INotificationTemplateRenderer templateRenderer,
        INotificationProvider notificationProvider,
        IPushNotificationProvider pushNotificationProvider,
        INotificationEventRepository repository,
        IDeviceTokenRepository deviceTokenRepository,
        ICustomerRepository customerRepository,
        IProviderRepository providerRepository,
        IMetricsService metricsService,
        ILogger<NotificationDispatchService> logger)
    {
        _templateRenderer = templateRenderer;
        _notificationProvider = notificationProvider;
        _pushNotificationProvider = pushNotificationProvider;
        _repository = repository;
        _deviceTokenRepository = deviceTokenRepository;
        _customerRepository = customerRepository;
        _providerRepository = providerRepository;
        _metricsService = metricsService;
        _logger = logger;
    }

    /// <summary>
    /// Task 277. The provider branch is the point of the row: a provider now
    /// has device tokens of its own (<see cref="DeviceTokenOwner"/>), so
    /// "tell the provider" is finally expressible.
    ///
    /// <para>
    /// <b>The owner kind decides which table is read, always.</b> It is never
    /// inferred from whether a lookup happened to find a row, so a provider id
    /// that collides with a customer id cannot resolve to that customer's
    /// phone number - the customer table is not consulted at all on the
    /// provider branch. Same reason
    /// <c>IDeviceTokenRepository.ListActiveByOwnerAsync</c> takes an owner
    /// rather than a bare Guid.
    /// </para>
    /// </summary>
    public async Task<NotificationRecipient> ResolveRecipientAsync(DeviceTokenOwner owner, CancellationToken cancellationToken = default)
    {
        var deviceTokens = (await _deviceTokenRepository.ListActiveByOwnerAsync(owner))
            .Select(t => t.Token)
            .ToList();

        switch (owner.Kind)
        {
            case DeviceTokenOwnerKind.Customer:
            {
                var customer = await _customerRepository.GetByIdAsync(owner.Id);
                if (customer is null)
                {
                    _logger.LogWarning("Customer {CustomerId} not found while resolving a notification recipient.", owner.Id);
                }

                return new NotificationRecipient(customer?.Mobile, customer?.Email, deviceTokens);
            }

            case DeviceTokenOwnerKind.Provider:
            {
                var provider = await _providerRepository.GetByIdAsync(owner.Id);
                if (provider is null)
                {
                    _logger.LogWarning("Provider {ProviderId} not found while resolving a notification recipient.", owner.Id);
                }

                // Provider.Phone is the SMS channel's address, mirroring
                // Customer.Mobile. It is the raw number on purpose: this is a
                // delivery address, not a template variable. Provider numbers
                // are masked where they would be *rendered* into a message
                // (BookingNotificationTriggerHandler.AddProviderVariablesAsync),
                // and NotificationDispatchService already masks whatever it
                // writes to the notification_event audit row.
                return new NotificationRecipient(provider?.Phone, provider?.Email, deviceTokens);
            }

            default:
                throw new NotSupportedException($"Device token owner kind {owner.Kind} has no recipient resolver wired up yet.");
        }
    }

    public async Task<IReadOnlyList<NotificationDispatchOutcome>> DispatchAsync(
        Guid customerId,
        NotificationEventType eventType,
        NotificationRecipient recipient,
        IReadOnlyDictionary<string, string> variables,
        Guid? bookingId = null,
        Guid? supportTicketId = null,
        CancellationToken cancellationToken = default)
    {
        var outcomes = new List<NotificationDispatchOutcome>();

        if (!string.IsNullOrWhiteSpace(recipient.Mobile))
        {
            outcomes.Add(await DispatchChannelAsync(
                customerId, eventType, NotificationChannel.Sms, recipient.Mobile, variables, bookingId, supportTicketId, cancellationToken));
        }

        if (!string.IsNullOrWhiteSpace(recipient.Email))
        {
            outcomes.Add(await DispatchChannelAsync(
                customerId, eventType, NotificationChannel.Email, recipient.Email, variables, bookingId, supportTicketId, cancellationToken));
        }

        // Task 156: one dispatch per registered device, not a single send -
        // a customer may have several (phone + tablet, or a reinstalled app
        // that registered a fresh token before the old one was revoked).
        if (recipient.PushDeviceTokens is { Count: > 0 } deviceTokens)
        {
            foreach (var deviceToken in deviceTokens)
            {
                outcomes.Add(await DispatchChannelAsync(
                    customerId, eventType, NotificationChannel.Push, deviceToken, variables, bookingId, supportTicketId, cancellationToken));
            }
        }

        return outcomes;
    }

    private async Task<NotificationDispatchOutcome> DispatchChannelAsync(
        Guid customerId,
        NotificationEventType eventType,
        NotificationChannel channel,
        string rawRecipient,
        IReadOnlyDictionary<string, string> variables,
        Guid? bookingId,
        Guid? supportTicketId,
        CancellationToken cancellationToken)
    {
        string payloadJson = JsonSerializer.Serialize(variables);

        if (!await _templateRenderer.SupportsChannelAsync(eventType, channel, cancellationToken))
        {
            _logger.LogWarning("No notification template registered for {EventType}/{Channel} - skipping dispatch.", eventType, channel);
            var untemplated = new NotificationEvent(Guid.NewGuid(), customerId, eventType, channel, ContactMasking.Mask(rawRecipient), "no_template", payloadJson, bookingId, supportTicketId);
            untemplated.MarkFailed("No template registered for this event/channel combination.");
            await _repository.AddAsync(untemplated);
            _metricsService.RecordNotificationOutcome(channel.ToString(), succeeded: false, untemplated.ErrorReason);
            return new NotificationDispatchOutcome(untemplated.Id, channel, untemplated.Status, untemplated.ErrorReason);
        }

        var rendered = await _templateRenderer.RenderAsync(eventType, channel, variables, cancellationToken);
        var notification = new NotificationEvent(Guid.NewGuid(), customerId, eventType, channel, ContactMasking.Mask(rawRecipient), rendered.TemplateKey, payloadJson, bookingId, supportTicketId);
        await _repository.AddAsync(notification);

        var sendResult = channel switch
        {
            NotificationChannel.Sms => await _notificationProvider.SendSmsAsync(rawRecipient, rendered.Body, cancellationToken),
            NotificationChannel.Email => await _notificationProvider.SendEmailAsync(rawRecipient, rendered.Subject ?? rendered.TemplateKey, rendered.Body, cancellationToken),
            NotificationChannel.Push => await _pushNotificationProvider.SendPushAsync(rawRecipient, rendered.Subject ?? rendered.TemplateKey, rendered.Body, cancellationToken),
            _ => throw new NotSupportedException($"Notification channel {channel} has no dispatcher wired up yet.")
        };

        if (sendResult.IsSuccess)
        {
            notification.MarkSent();
        }
        else
        {
            notification.MarkFailed(sendResult.Error.Message);
        }

        await _repository.UpdateAsync(notification);
        _metricsService.RecordNotificationOutcome(channel.ToString(), sendResult.IsSuccess, notification.ErrorReason);

        return new NotificationDispatchOutcome(notification.Id, channel, notification.Status, notification.ErrorReason);
    }
}

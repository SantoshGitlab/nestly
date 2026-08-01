using System.Text.Json;
using Microsoft.Extensions.Logging;
using Nestly.Application.Abstractions.Observability;
using Nestly.Application.Notifications;
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
    private readonly IMetricsService _metricsService;
    private readonly ILogger<NotificationDispatchService> _logger;

    public NotificationDispatchService(
        INotificationTemplateRenderer templateRenderer,
        INotificationProvider notificationProvider,
        IPushNotificationProvider pushNotificationProvider,
        INotificationEventRepository repository,
        IMetricsService metricsService,
        ILogger<NotificationDispatchService> logger)
    {
        _templateRenderer = templateRenderer;
        _notificationProvider = notificationProvider;
        _pushNotificationProvider = pushNotificationProvider;
        _repository = repository;
        _metricsService = metricsService;
        _logger = logger;
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
            var untemplated = new NotificationEvent(Guid.NewGuid(), customerId, eventType, channel, Mask(rawRecipient), "no_template", payloadJson, bookingId, supportTicketId);
            untemplated.MarkFailed("No template registered for this event/channel combination.");
            await _repository.AddAsync(untemplated);
            _metricsService.RecordNotificationOutcome(channel.ToString(), succeeded: false, untemplated.ErrorReason);
            return new NotificationDispatchOutcome(untemplated.Id, channel, untemplated.Status, untemplated.ErrorReason);
        }

        var rendered = await _templateRenderer.RenderAsync(eventType, channel, variables, cancellationToken);
        var notification = new NotificationEvent(Guid.NewGuid(), customerId, eventType, channel, Mask(rawRecipient), rendered.TemplateKey, payloadJson, bookingId, supportTicketId);
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

    /// <summary>Same masking convention as <see cref="SandboxNotificationProvider"/> - the stored Recipient is for audit/dedup, never a fully readable contact.</summary>
    private static string Mask(string value)
    {
        if (value.Length <= 4)
        {
            return new string('*', value.Length);
        }

        return new string('*', value.Length - 4) + value[^4..];
    }
}

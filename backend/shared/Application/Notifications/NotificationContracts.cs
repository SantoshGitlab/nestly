using Nestly.Domain;

namespace Nestly.Application.Notifications;

/// <summary>A rendered, channel-ready message (task 87b).</summary>
public record RenderedNotification(string TemplateKey, string? Subject, string Body);

/// <summary>
/// Contact details to dispatch to (task 87c). Mobile/Email may be null, in
/// which case that channel is simply skipped. PushDeviceTokens (task 156)
/// fans out to one dispatch per registered device rather than a single
/// send, since a customer may have several.
/// </summary>
public record NotificationRecipient(string? Mobile, string? Email, IReadOnlyList<string>? PushDeviceTokens = null);

/// <summary>The logged outcome of one channel's dispatch attempt (task 87d).</summary>
public record NotificationDispatchOutcome(Guid NotificationEventId, NotificationChannel Channel, NotificationDeliveryStatus Status, string? ErrorReason);

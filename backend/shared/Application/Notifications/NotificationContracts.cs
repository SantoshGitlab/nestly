using Nestly.Domain;

namespace Nestly.Application.Notifications;

/// <summary>A rendered, channel-ready message (task 87b).</summary>
public record RenderedNotification(string TemplateKey, string? Subject, string Body);

/// <summary>Contact details to dispatch to - either may be null, in which case that channel is simply skipped (task 87c).</summary>
public record NotificationRecipient(string? Mobile, string? Email);

/// <summary>The logged outcome of one channel's dispatch attempt (task 87d).</summary>
public record NotificationDispatchOutcome(Guid NotificationEventId, NotificationChannel Channel, NotificationDeliveryStatus Status, string? ErrorReason);

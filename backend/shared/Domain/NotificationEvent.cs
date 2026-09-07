using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>
/// A logged notification send attempt (SRS 19.2, tasks 84d, 87a, 87d). One
/// row per (event, recipient, channel) dispatch - a single trigger that
/// fans out to multiple channels (SMS + email, say) creates one
/// <see cref="NotificationEvent"/> per channel, so each channel's delivery
/// status is tracked independently.
///
/// <see cref="BookingId"/>/<see cref="SupportTicketId"/> are both optional
/// and mutually exclusive in practice - whichever domain event triggered the
/// notification sets the one that applies (see task 88's trigger wiring);
/// neither is a hard constraint here since some events (e.g. Welcome) set
/// neither.
/// </summary>
public class NotificationEvent : Entity<Guid>
{
    public Guid CustomerId { get; private set; }
    public Guid? BookingId { get; private set; }
    public Guid? SupportTicketId { get; private set; }

    public NotificationEventType EventType { get; private set; }
    public NotificationChannel Channel { get; private set; }

    /// <summary>Masked/redacted recipient (mobile, email, or device token id) - never the raw OTP/payload, matching the no-PII-in-plaintext-logs rule <see cref="SandboxNotificationProvider"/> already follows.</summary>
    public string Recipient { get; private set; } = string.Empty;

    public string TemplateKey { get; private set; } = string.Empty;

    /// <summary>Serialized variables used to render the template (SRS 19.2 "payload/variables reference") - for audit/debugging, never re-rendered from this on delivery.</summary>
    public string? PayloadJson { get; private set; }

    public NotificationDeliveryStatus Status { get; private set; }
    public string? ErrorReason { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? SentAtUtc { get; private set; }

    protected NotificationEvent() { }

    public NotificationEvent(
        Guid id,
        Guid customerId,
        NotificationEventType eventType,
        NotificationChannel channel,
        string recipient,
        string templateKey,
        string? payloadJson,
        Guid? bookingId = null,
        Guid? supportTicketId = null)
        : base(id)
    {
        CustomerId = customerId;
        EventType = eventType;
        Channel = channel;
        Recipient = string.IsNullOrWhiteSpace(recipient)
            ? throw new ArgumentException("Recipient is required.", nameof(recipient))
            : recipient;
        TemplateKey = string.IsNullOrWhiteSpace(templateKey)
            ? throw new ArgumentException("Template key is required.", nameof(templateKey))
            : templateKey;
        PayloadJson = payloadJson;
        BookingId = bookingId;
        SupportTicketId = supportTicketId;
        Status = NotificationDeliveryStatus.Pending;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void MarkSent()
    {
        Status = NotificationDeliveryStatus.Sent;
        SentAtUtc = DateTime.UtcNow;
    }

    public void MarkFailed(string errorReason)
    {
        Status = NotificationDeliveryStatus.Failed;
        ErrorReason = errorReason;
        SentAtUtc = DateTime.UtcNow;
    }
}

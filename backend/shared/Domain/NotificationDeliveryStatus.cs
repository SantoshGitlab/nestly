namespace Nestly.Domain;

/// <summary>Delivery outcome of a dispatched notification (SRS 19.2).</summary>
public enum NotificationDeliveryStatus
{
    Pending,
    Sent,
    Failed
}

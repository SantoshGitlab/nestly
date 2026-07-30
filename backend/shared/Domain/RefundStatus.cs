namespace Nestly.Domain;

/// <summary>Refund status lifecycle (SRS 31.3, 11.17.2, task 75d).</summary>
public enum RefundStatus
{
    Initiated,
    Processing,
    Refunded,
    Failed
}

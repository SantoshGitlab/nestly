namespace Nestly.Domain;

/// <summary>Whether a refund covers the full original payment or only part of it (SRS 14.4).</summary>
public enum RefundType
{
    Full,
    Partial
}

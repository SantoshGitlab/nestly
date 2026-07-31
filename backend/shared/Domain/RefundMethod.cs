namespace Nestly.Domain;

/// <summary>
/// How a refund is settled (SRS 14.4 - "gateway refund, wallet credit, or
/// mixed model"). A mixed/split settlement across both is not modelled here -
/// deliberately out of scope for this phase; each refund resolves through
/// exactly one channel.
/// </summary>
public enum RefundMethod
{
    Gateway,
    Wallet
}

namespace Nestly.Domain;

/// <summary>
/// A customer subscription's lifecycle status (PRODUCT-ENHANCEMENTS.md #1's
/// data model: "active/cancelled/expired/payment_failed"). <see cref="PaymentFailed"/>
/// is a temporary, recoverable suspension - a failed recurring charge moves a
/// subscription here (benefits paused) while it retries with backoff, not
/// straight to <see cref="Expired"/> ("a subscriber shouldn't lose an active
/// plan over one declined card without a chance to fix payment details").
/// <see cref="Expired"/> is the terminal state once retries are exhausted;
/// <see cref="Cancelled"/> is the terminal state for a customer-initiated
/// cancellation. Both are dead ends - see <see cref="CustomerSubscription.Cancel"/>
/// and <see cref="CustomerSubscription.RecordFailedCharge"/>.
/// </summary>
public enum CustomerSubscriptionStatus
{
    Active,
    Cancelled,
    Expired,
    PaymentFailed
}

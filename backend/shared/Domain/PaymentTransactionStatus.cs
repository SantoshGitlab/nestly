namespace Nestly.Domain;

/// <summary>
/// Lifecycle of a <see cref="PaymentTransaction"/> - the 1:1 record for a
/// booking's payment (SRS 11.11.3 "booking-payment mapping shall be
/// preserved"). Individual gateway round-trips are <see cref="PaymentAttempt"/>
/// rows underneath this; this status reflects the outcome of the most recent
/// attempt.
/// </summary>
public enum PaymentTransactionStatus
{
    /// <summary>An attempt is in flight (order created, awaiting the gateway callback) or about to be retried.</summary>
    Pending,
    Success,

    /// <summary>The most recent attempt failed. A new attempt may still be started (task 70 - retry preserves booking intent).</summary>
    Failed,
    Cancelled
}

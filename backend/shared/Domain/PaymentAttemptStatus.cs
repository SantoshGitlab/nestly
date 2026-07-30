namespace Nestly.Domain;

/// <summary>Lifecycle of a single <see cref="PaymentAttempt"/> - one gateway order round-trip.</summary>
public enum PaymentAttemptStatus
{
    /// <summary>A gateway order was created; the customer has not completed (or the gateway has not confirmed) payment yet.</summary>
    Created,
    Success,
    Failed
}

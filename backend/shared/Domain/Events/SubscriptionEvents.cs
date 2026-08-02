using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain.Events;

/// <summary>A recurring charge succeeded and the subscription rolled to its next period (task 178, 183).</summary>
public sealed record SubscriptionRenewedEvent(Guid SubscriptionId, Guid CustomerId) : DomainEvent;

/// <summary>A recurring charge failed (task 178, 183). <paramref name="IsFinal"/> distinguishes a recoverable suspension (still retrying, PaymentFailed) from the terminal outcome once retries are exhausted (Expired) - both are reported through the same notification event type (SubscriptionPaymentFailed) with different wording, rather than adding a fourth event/notification type for what is still fundamentally "your payment failed."</summary>
public sealed record SubscriptionPaymentFailedEvent(Guid SubscriptionId, Guid CustomerId, bool IsFinal) : DomainEvent;

/// <summary>The subscription's next billing attempt is within the reminder window (task 183).</summary>
public sealed record SubscriptionExpiringSoonEvent(Guid SubscriptionId, Guid CustomerId) : DomainEvent;

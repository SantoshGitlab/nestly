using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>
/// A refund against a booking's payment (SRS 23.4 refund_transaction, SRS
/// 14.4). Always references the booking and, where applicable, the original
/// payment transaction it is refunding - a wallet-settled refund still keeps
/// that reference for reconciliation (SRS 14.3) even though no gateway call
/// is made for it.
/// </summary>
public class RefundTransaction : AggregateRoot<Guid>
{
    public Guid BookingId { get; private set; }

    public Guid PaymentTransactionId { get; private set; }

    public RefundType Type { get; private set; }

    public RefundMethod Method { get; private set; }

    public decimal Amount { get; private set; }

    public RefundStatus Status { get; private set; }

    public string? GatewayRefundRef { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? ProcessedAtUtc { get; private set; }

    protected RefundTransaction() { }

    public RefundTransaction(
        Guid id, Guid bookingId, Guid paymentTransactionId, RefundType type, RefundMethod method, decimal amount, string reason)
        : base(id)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Refund amount must be positive.");
        }

        BookingId = bookingId;
        PaymentTransactionId = paymentTransactionId;
        Type = type;
        Method = method;
        Amount = amount;
        Reason = reason ?? throw new ArgumentException("Refund reason is required.", nameof(reason));
        Status = RefundStatus.Initiated;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void MarkProcessing() => TransitionTo(RefundStatus.Processing);

    public void MarkRefunded(string? gatewayRefundRef)
    {
        TransitionTo(RefundStatus.Refunded);
        GatewayRefundRef = gatewayRefundRef;
        ProcessedAtUtc = DateTime.UtcNow;
    }

    public void MarkFailed(string reason)
    {
        TransitionTo(RefundStatus.Failed);
        Reason = reason;
        ProcessedAtUtc = DateTime.UtcNow;
    }

    private void TransitionTo(RefundStatus newStatus)
    {
        if (!RefundTransactionLifecycle.IsValidTransition(Status, newStatus))
        {
            throw new InvalidOperationException($"Cannot transition a refund from {Status} to {newStatus}.");
        }

        Status = newStatus;
    }
}

using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>
/// A refund against one of a booking's funding sources (SRS 23.4
/// refund_transaction, SRS 14.4). Always references the booking; whether it
/// also references a <see cref="PaymentTransaction"/> depends on
/// <see cref="FundingSource"/> - a refund of wallet balance the booking
/// consumed at checkout has no gateway payment to point at, because that
/// money never went through a gateway (see <see cref="RefundFundingSource"/>).
/// A payment-funded refund keeps its <see cref="PaymentTransactionId"/> even
/// when it is settled to the wallet, for reconciliation (SRS 14.3).
///
/// One row models exactly one funding source, so a booking paid part-wallet/
/// part-gateway produces two rows for one refund request - see
/// <c>RefundService</c>. Modelling it as one row would need
/// <see cref="Method"/> to be per-portion, which is exactly the mixed
/// settlement <see cref="RefundMethod"/> deliberately does not model.
/// </summary>
public class RefundTransaction : AggregateRoot<Guid>
{
    public Guid BookingId { get; private set; }

    /// <summary>The gateway payment being refunded, or null when <see cref="FundingSource"/> is <see cref="RefundFundingSource.Wallet"/>.</summary>
    public Guid? PaymentTransactionId { get; private set; }

    public RefundFundingSource FundingSource { get; private set; }

    public RefundType Type { get; private set; }

    public RefundMethod Method { get; private set; }

    public decimal Amount { get; private set; }

    public RefundStatus Status { get; private set; }

    public string? GatewayRefundRef { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? ProcessedAtUtc { get; private set; }

    protected RefundTransaction() { }

    private RefundTransaction(
        Guid id, Guid bookingId, Guid? paymentTransactionId, RefundFundingSource fundingSource,
        RefundType type, RefundMethod method, decimal amount, string reason)
        : base(id)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Refund amount must be positive.");
        }

        BookingId = bookingId;
        PaymentTransactionId = paymentTransactionId;
        FundingSource = fundingSource;
        Type = type;
        Method = method;
        Amount = amount;
        Reason = reason ?? throw new ArgumentException("Refund reason is required.", nameof(reason));
        Status = RefundStatus.Initiated;
        CreatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Refunds part or all of the booking's gateway payment, settled through <paramref name="method"/> (the gateway itself, or as wallet credit).</summary>
    public static RefundTransaction ForPayment(
        Guid id, Guid bookingId, Guid paymentTransactionId, RefundType type, RefundMethod method, decimal amount, string reason) =>
        new(id, bookingId, paymentTransactionId, RefundFundingSource.Payment, type, method, amount, reason);

    /// <summary>
    /// Refunds part or all of the wallet balance the booking consumed at
    /// checkout. Always settled back to the wallet - there is no gateway
    /// payment to reverse, so <see cref="RefundMethod.Gateway"/> is not a
    /// choice the caller gets to make here.
    /// </summary>
    public static RefundTransaction ForWalletCredit(Guid id, Guid bookingId, RefundType type, decimal amount, string reason) =>
        new(id, bookingId, paymentTransactionId: null, RefundFundingSource.Wallet, type, RefundMethod.Wallet, amount, reason);

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

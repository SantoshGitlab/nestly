using Nestly.Domain;

namespace Nestly.Application.Refunds;

/// <summary>
/// One refund settlement's current state (SRS 11.17.2 "refund status visible
/// against booking"; SRS 31.3 lifecycle). <paramref name="PaymentTransactionId"/>
/// is null on a wallet-funded settlement - see <see cref="RefundFundingSource"/>.
/// </summary>
public record RefundTransactionResponse(
    Guid Id,
    Guid BookingId,
    Guid? PaymentTransactionId,
    RefundFundingSource FundingSource,
    RefundType Type,
    RefundMethod Method,
    decimal Amount,
    RefundStatus Status,
    string? GatewayRefundRef,
    string Reason,
    DateTime CreatedAtUtc,
    DateTime? ProcessedAtUtc);

/// <summary>
/// The outcome of one refund request. A booking can be funded from two
/// independent sources - its gateway payment and the wallet balance it
/// consumed at checkout - and one <see cref="RefundTransaction"/> models
/// exactly one of them, so a request that spans both settles as two rows
/// (task 356). <paramref name="TotalAmount"/> is what the customer actually
/// gets back across all of them.
/// </summary>
public sealed record RefundOutcomeResponse(
    Guid BookingId,
    decimal TotalAmount,
    IReadOnlyList<RefundTransactionResponse> Settlements)
{
    /// <summary>
    /// The settlement a caller that can only record one refund reference
    /// links to (<c>BookingCancellation.RefundTransactionId</c>, the dispute
    /// resolution record). Payment-funded first when there is one, since that
    /// is the settlement a customer chasing "where is my money" is asking
    /// about; the rest are always discoverable through the booking.
    /// </summary>
    public RefundTransactionResponse Primary => Settlements[0];
}

using Nestly.Domain;

namespace Nestly.Application.Refunds;

/// <summary>A refund's current state (SRS 11.17.2 "refund status visible against booking"; SRS 31.3 lifecycle).</summary>
public record RefundTransactionResponse(
    Guid Id,
    Guid BookingId,
    Guid PaymentTransactionId,
    RefundType Type,
    RefundMethod Method,
    decimal Amount,
    RefundStatus Status,
    string? GatewayRefundRef,
    string Reason,
    DateTime CreatedAtUtc,
    DateTime? ProcessedAtUtc);

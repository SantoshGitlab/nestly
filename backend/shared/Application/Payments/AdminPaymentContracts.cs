using Nestly.Domain;

namespace Nestly.Application.Payments;

/// <summary>
/// Admin payment transaction view (SRS 12.13.1, task 311) - a reconciliation
/// list/detail surface over the same <see cref="PaymentTransaction"/>/
/// <see cref="PaymentAttempt"/>/<see cref="RefundTransaction"/> data
/// <see cref="IPaymentService.GetByBookingIdAsync"/> already exposes to the
/// owning customer and <c>BookingManagementService</c> already embeds inside
/// a booking's admin detail view. This is the missing standalone surface:
/// list every transaction (filterable by status and booking), and a detail
/// view that adds refund history alongside the attempts
/// <see cref="PaymentTransactionResponse"/> already carries. Read-only by
/// design - see <see cref="AdminModules.Payments"/>'s doc comment for why.
/// </summary>
public sealed record AdminPaymentTransactionFilterRequest(
    Guid? BookingId = null,
    PaymentTransactionStatus? Status = null,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    int Page = 1,
    int PageSize = 20);

/// <summary>One row of the admin payment transaction list (SRS 12.13.1's field list, minus the per-attempt detail a list row has no room for).</summary>
public sealed record AdminPaymentTransactionListItemResponse(
    Guid Id,
    Guid BookingId,
    Guid CustomerId,
    decimal Amount,
    string Currency,
    PaymentTransactionStatus Status,
    string? LatestGatewayOrderId,
    string? LatestGatewayPaymentRef,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record PagedAdminPaymentTransactionResponse(
    IReadOnlyList<AdminPaymentTransactionListItemResponse> Items, int TotalCount, int Page, int PageSize);

/// <summary>One refund raised against the transaction, for the detail view's reconciliation trail.</summary>
public sealed record AdminRefundTransactionResponse(
    Guid Id,
    RefundType Type,
    RefundMethod Method,
    decimal Amount,
    RefundStatus Status,
    string? GatewayRefundRef,
    string Reason,
    DateTime CreatedAtUtc,
    DateTime? ProcessedAtUtc);

/// <summary>
/// Full transaction detail for the admin view (SRS 12.13.1) - every gateway
/// round-trip (<see cref="PaymentAttemptResponse"/>, same shape the customer
/// side already returns) plus every refund raised against it, for
/// reconciliation (SRS 14.3).
/// </summary>
public sealed record AdminPaymentTransactionDetailResponse(
    Guid Id,
    Guid BookingId,
    Guid CustomerId,
    decimal Amount,
    string Currency,
    PaymentTransactionStatus Status,
    IReadOnlyList<PaymentAttemptResponse> Attempts,
    IReadOnlyList<AdminRefundTransactionResponse> Refunds,
    decimal? CommissionRatePercentage,
    decimal? CommissionAmount,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

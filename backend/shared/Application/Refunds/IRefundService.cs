using Nestly.BuildingBlocks.Results;
using Nestly.Domain;

namespace Nestly.Application.Refunds;

/// <summary>
/// Refund initiation and tracking (SRS 11.17.2, 14.4, tasks 75a-d). Not
/// customer-facing to *initiate* - a refund is always triggered by a
/// cancellation flow or an admin action, neither of which exist yet (Phase
/// 5/6); these methods are the capability those flows will call. Customers
/// can only *view* refund status (SRS 11.17.2), via <see cref="ListByBookingAsync"/>.
/// </summary>
public interface IRefundService
{
    /// <summary>Refunds whatever remains unrefunded on the booking's successful payment in full.</summary>
    Task<Result<RefundTransactionResponse>> InitiateFullRefundAsync(Guid bookingId, string reason, RefundMethod method = RefundMethod.Gateway);

    /// <summary>Refunds a specific amount, which must not exceed what remains unrefunded (task 75c policy rule).</summary>
    Task<Result<RefundTransactionResponse>> InitiatePartialRefundAsync(Guid bookingId, decimal amount, string reason, RefundMethod method = RefundMethod.Gateway);

    /// <summary>Refund history for a booking the caller owns (SRS 11.17.2).</summary>
    Task<Result<IReadOnlyList<RefundTransactionResponse>>> ListByBookingAsync(Guid customerId, Guid bookingId);
}

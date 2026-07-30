using Nestly.Domain;

namespace Nestly.Application.Refunds;

public interface IRefundTransactionRepository
{
    Task AddAsync(RefundTransaction refund);

    Task<RefundTransaction?> GetByIdAsync(Guid id);

    /// <summary>Refund history for a booking, newest first (SRS 11.17.2).</summary>
    Task<IReadOnlyList<RefundTransaction>> ListByBookingAsync(Guid bookingId);

    /// <summary>Every refund (of any status) ever raised against a payment - used to compute how much of it remains refundable (task 75c).</summary>
    Task<IReadOnlyList<RefundTransaction>> ListByPaymentTransactionAsync(Guid paymentTransactionId);
}

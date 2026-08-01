using Nestly.Domain;

namespace Nestly.Application.Payments;

public interface IPaymentTransactionRepository
{
    Task AddAsync(PaymentTransaction transaction);

    /// <summary>
    /// Inserts a brand-new transaction, but returns <c>false</c> instead of
    /// throwing if another concurrent request already created one for the
    /// same booking (task 135b - BookingId's unique index is the guard).
    /// Callers must not retry with the same instance; on a <c>false</c>
    /// return, re-read via <see cref="GetByBookingIdAsync"/> to get the
    /// transaction that actually won.
    /// </summary>
    Task<bool> TryAddAsync(PaymentTransaction transaction);

    Task UpdateAsync(PaymentTransaction transaction);

    /// <summary>Loaded with its attempts - a transaction is never useful partially loaded.</summary>
    Task<PaymentTransaction?> GetByIdAsync(Guid id);

    /// <summary>A booking has at most one payment transaction (task 69c - booking-payment mapping).</summary>
    Task<PaymentTransaction?> GetByBookingIdAsync(Guid bookingId);

    /// <summary>Used by the webhook handler to resolve which attempt a callback belongs to.</summary>
    Task<PaymentTransaction?> GetByGatewayOrderIdAsync(string gatewayOrderId);

    /// <summary>Reconciliation support (SRS 14.3, task 71) - every transaction record, newest first.</summary>
    Task<IReadOnlyList<PaymentTransaction>> ListAsync(DateTime? fromUtc, DateTime? toUtc, PaymentTransactionStatus? status);
}

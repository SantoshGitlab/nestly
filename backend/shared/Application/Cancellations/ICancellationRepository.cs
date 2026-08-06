using Nestly.Domain;

namespace Nestly.Application.Cancellations;

public interface ICancellationRepository
{
    Task AddAsync(BookingCancellation cancellation);

    /// <summary>
    /// Inserts a brand-new cancellation row, but returns <c>false</c> instead
    /// of throwing if another concurrent request already reserved this
    /// booking's one-and-only cancellation (NESTLY-002 - BookingId's unique
    /// index, BookingCancellationConfiguration, is the guard). Mirrors
    /// <c>IPaymentTransactionRepository.TryAddAsync</c>. Callers must not
    /// retry with the same instance; on a <c>false</c> return, re-read via
    /// <see cref="GetByBookingIdAsync"/> to get the row that actually won.
    /// </summary>
    Task<bool> TryAddAsync(BookingCancellation cancellation);

    /// <summary>Persists <see cref="BookingCancellation.AttachRefund"/> once the winner's refund has actually been raised.</summary>
    Task UpdateAsync(BookingCancellation cancellation);

    Task<BookingCancellation?> GetByBookingIdAsync(Guid bookingId);
}

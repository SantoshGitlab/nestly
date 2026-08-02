using Nestly.Domain;

namespace Nestly.Application.Bookings;

/// <summary>Persistence for <see cref="BookingCompletionProof"/> (task 195). One row per booking - see the entity's own doc comment.</summary>
public interface IBookingCompletionProofRepository
{
    Task AddAsync(BookingCompletionProof proof);

    Task UpdateAsync(BookingCompletionProof proof);

    Task<BookingCompletionProof?> GetByBookingIdAsync(Guid bookingId);

    /// <summary>Used by the task 196 completion guard - cheaper than a full load when only presence matters.</summary>
    Task<bool> ExistsForBookingAsync(Guid bookingId);
}

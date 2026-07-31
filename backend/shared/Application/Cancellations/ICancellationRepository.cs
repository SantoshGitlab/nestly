using Nestly.Domain;

namespace Nestly.Application.Cancellations;

public interface ICancellationRepository
{
    Task AddAsync(BookingCancellation cancellation);

    Task<BookingCancellation?> GetByBookingIdAsync(Guid bookingId);
}

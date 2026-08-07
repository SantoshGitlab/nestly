using Nestly.Domain;

namespace Nestly.Application;

/// <summary>
/// The one-row-per-booking live tracking state (task 271). Keyed on the
/// booking rather than on the row's own id in every read, because no caller
/// ever holds a <see cref="BookingTracking.Id"/> - they hold a booking.
/// </summary>
public interface IBookingTrackingRepository
{
    /// <summary>The booking's tracking row, or null when nothing has ever been tracked for it.</summary>
    Task<BookingTracking?> GetByBookingAsync(Guid bookingId);

    Task AddAsync(BookingTracking entity);

    Task UpdateAsync(BookingTracking entity);
}

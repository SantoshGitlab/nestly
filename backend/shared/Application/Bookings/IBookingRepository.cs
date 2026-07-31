using Nestly.Domain;

namespace Nestly.Application.Bookings;

public interface IBookingRepository
{
    Task AddAsync(Booking booking);

    Task UpdateAsync(Booking booking);

    /// <summary>Loaded with its items, their add-ons, and its status history - a booking is never useful partially loaded.</summary>
    Task<Booking?> GetByIdAsync(Guid id);

    /// <summary><paramref name="statuses"/> is the expansion of a <see cref="BookingStatusBucket"/> via <see cref="BookingStatusMapper.StatusesInBucket"/>.</summary>
    Task<IReadOnlyList<Booking>> ListByCustomerAsync(Guid customerId, IReadOnlyList<BookingStatus> statuses);

    /// <summary>Filterable, paginated admin search across every booking (SRS 12.11.1, task 115a).</summary>
    Task<BookingSearchResult> SearchAsync(BookingSearchFilter filter);
}

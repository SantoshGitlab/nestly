using Nestly.Domain;

namespace Nestly.Application.Reschedules;

public interface IRescheduleRepository
{
    Task AddAsync(BookingReschedule reschedule);

    Task<IReadOnlyList<BookingReschedule>> ListByBookingAsync(Guid bookingId);

    /// <summary>How many times this booking has already been rescheduled (task 82b count limit).</summary>
    Task<int> CountByBookingAsync(Guid bookingId);
}

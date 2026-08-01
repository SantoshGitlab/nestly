using Nestly.Domain;

namespace Nestly.Application;

/// <summary>Persistence for <see cref="BookingPartnerAssignment"/> (task 147).</summary>
public interface IBookingPartnerAssignmentRepository
{
    Task AddAsync(BookingPartnerAssignment entity);
    Task UpdateAsync(BookingPartnerAssignment entity);
    Task<BookingPartnerAssignment?> GetByIdAsync(Guid id);

    /// <summary>The currently outstanding assignment for a booking (status Assigned or Accepted), or null if none - PARTNER.md OPEN DECISIONS #5, only one row is ever "live" at a time.</summary>
    Task<BookingPartnerAssignment?> GetActiveByBookingAsync(Guid bookingId);

    /// <summary>Full assignment history for a booking, newest first (task 159 - shows prior rejections leading to the current state).</summary>
    Task<IReadOnlyList<BookingPartnerAssignment>> ListByBookingAsync(Guid bookingId);
}

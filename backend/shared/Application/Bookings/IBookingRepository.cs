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

    /// <summary>Bookings currently assigned to a partner (<see cref="Booking.AssignedPartnerId"/>), for the admin performance view (task 150c). Reflects the live assignment only - a booking whose assignment was later rejected/reassigned away from this partner will no longer appear here, even though <c>BookingPartnerAssignment</c> retains that history.</summary>
    Task<IReadOnlyList<Booking>> ListByAssignedPartnerAsync(Guid partnerId);

    /// <summary>Nestly Coins' reorder check (docs/NESTLY-COINS.md GUIDELINES #2, task 201): does this customer have any OTHER Completed booking besides <paramref name="excludingBookingId"/>? A dedicated count query, mirroring <c>IReferralRepository.CountRewardedByReferrerAsync</c>'s convention, rather than listing and filtering full booking rows client-side.</summary>
    Task<int> CountCompletedByCustomerAsync(Guid customerId, Guid excludingBookingId);

    /// <summary>Nestly Coins' reorder check, partner side (docs/NESTLY-COINS.md GUIDELINES #2, task 201): does this partner have any OTHER Completed booking besides <paramref name="excludingBookingId"/>?</summary>
    Task<int> CountCompletedByAssignedPartnerAsync(Guid partnerId, Guid excludingBookingId);
}

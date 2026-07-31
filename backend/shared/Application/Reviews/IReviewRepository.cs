using Nestly.Domain;

namespace Nestly.Application.Reviews;

public interface IReviewRepository
{
    Task AddAsync(Review review);

    Task<Review?> GetByBookingIdAsync(Guid bookingId);

    Task<IReadOnlyList<Review>> ListByServiceAsync(Guid serviceId);
}

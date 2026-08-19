using Nestly.Domain;

namespace Nestly.Application.CustomerRatings;

public interface ICustomerRatingRepository
{
    Task AddAsync(CustomerRating rating);

    Task<CustomerRating?> GetByBookingIdAsync(Guid bookingId);

    /// <summary>Aggregated in the database, same rationale as <c>IReviewRepository.GetProviderRatingAsync</c> - null, not a zero-filled row, when the customer has no ratings yet.</summary>
    Task<CustomerRatingSummary?> GetSummaryForCustomerAsync(Guid customerId, CancellationToken cancellationToken = default);

    /// <summary>The most recent ratings for the Customer 360 view, newest first, capped by <paramref name="limit"/>.</summary>
    Task<IReadOnlyList<CustomerRatingRow>> ListRecentForCustomerAsync(Guid customerId, int limit, CancellationToken cancellationToken = default);
}

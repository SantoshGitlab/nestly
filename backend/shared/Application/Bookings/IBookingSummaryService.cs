using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.Bookings;

/// <summary>Computes a booking summary/preview (SRS 11.7) without persisting anything.</summary>
public interface IBookingSummaryService
{
    /// <summary><paramref name="customerId"/> scopes address ownership - a customer can never preview a booking against another customer's address.</summary>
    Task<Result<BookingSummaryResponse>> GetSummaryAsync(Guid customerId, BookingSummaryRequest request);
}

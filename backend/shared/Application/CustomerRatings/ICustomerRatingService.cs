using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.CustomerRatings;

/// <summary>Provider-side rating submission for a completed job - the reverse-direction analogue of <c>IReviewService</c>.</summary>
public interface ICustomerRatingService
{
    /// <summary>Whether this job can be rated right now - the booking is the caller's, Completed, and not already rated.</summary>
    Task<Result<CustomerRatingEligibilityResponse>> GetEligibilityAsync(Guid providerId, Guid bookingId);

    /// <summary>The rating already submitted for this job, if any.</summary>
    Task<Result<CustomerRatingResponse?>> GetByBookingAsync(Guid providerId, Guid bookingId);

    /// <summary>Submits the job's one rating of the customer.</summary>
    Task<Result<CustomerRatingResponse>> SubmitAsync(Guid providerId, Guid bookingId, SubmitCustomerRatingRequest request);
}

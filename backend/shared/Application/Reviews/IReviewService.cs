using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.Reviews;

/// <summary>Review submission eligibility and confirmation (SRS 11.16, 17, tasks 85a-c).</summary>
public interface IReviewService
{
    /// <summary>Whether this booking can be reviewed right now - completed-status, not-already-reviewed, and submission-window checks.</summary>
    Task<Result<ReviewEligibilityResponse>> GetEligibilityAsync(Guid customerId, Guid bookingId);

    /// <summary>The review already submitted for this booking, if any.</summary>
    Task<Result<ReviewResponse?>> GetByBookingAsync(Guid customerId, Guid bookingId);

    /// <summary>Submits the booking's one primary review.</summary>
    Task<Result<ReviewResponse>> SubmitAsync(Guid customerId, Guid bookingId, SubmitReviewRequest request);
}

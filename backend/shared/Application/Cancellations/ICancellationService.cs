using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.Cancellations;

/// <summary>
/// Cancellation eligibility, fee/refund preview, and confirmation (SRS
/// 11.14, 32.2, tasks 80a-c, 81).
/// </summary>
public interface ICancellationService
{
    /// <summary>Eligibility + fee/refund preview for the caller to review before confirming (SRS 11.14.3).</summary>
    Task<Result<CancellationPolicyResponse>> GetPolicyAsync(Guid customerId, Guid bookingId);

    /// <summary>Confirms the cancellation: transitions the booking, raises a refund if one is owed, and records the cancellation (SRS 24.6).</summary>
    Task<Result<CancellationOutcomeResponse>> CancelAsync(Guid customerId, Guid bookingId, CancelBookingRequest request);
}

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

    /// <summary>
    /// Admin-initiated cancellation (SRS 12.11.3, task 117a). Not scoped to a
    /// customer id - the caller (<c>BookingManagementService</c>) has already
    /// verified the requesting admin holds "bookings.write" - and checks
    /// eligibility against <see cref="Domain.BookingStatus.CancelledByAdmin"/>
    /// rather than CancelledByAdmin's customer-initiated sibling, so an admin
    /// can cancel a booking already <see cref="Domain.BookingStatus.InProgress"/>
    /// (SRS 13.1's lifecycle only allows that transition for an admin, never
    /// a customer). Reuses the exact same fee/refund computation as
    /// <see cref="CancelAsync"/> - only the target status, actor, and
    /// eligibility check differ.
    /// </summary>
    Task<Result<CancellationOutcomeResponse>> AdminCancelAsync(Guid bookingId, string reason, string? internalNotes = null);
}

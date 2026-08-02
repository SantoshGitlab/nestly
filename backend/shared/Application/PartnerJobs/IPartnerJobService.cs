using Nestly.Application.Bookings;
using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.PartnerJobs;

/// <summary>
/// The partner-facing job lifecycle (task 149a, PARTNER.md API surface
/// "Jobs" - list/detail, accept/reject/start/complete, completion proof).
/// Every method takes the caller's own partner id (resolved from the JWT by
/// the controller, never a route/body value - SRS 28.3 IDOR) and reads/
/// writes through the same <see cref="Nestly.Domain.BookingPartnerAssignment"/>
/// bridge entity task 147 introduced, delegating accept/reject to
/// <see cref="Nestly.Application.PartnerManagement.IBookingPartnerAssignmentService"/>
/// so the assignment lifecycle has exactly one owner.
/// </summary>
public interface IPartnerJobService
{
    Task<Result<PartnerJobSearchResponse>> ListAsync(Guid partnerId, PartnerJobStatus? status, DateOnly? date);

    Task<Result<PartnerJobDetailResponse>> GetDetailAsync(Guid partnerId, Guid bookingId);

    Task<Result<PartnerJobDetailResponse>> AcceptAsync(Guid partnerId, Guid bookingId);

    Task<Result<PartnerJobDetailResponse>> RejectAsync(Guid partnerId, Guid bookingId, RejectJobRequest request);

    /// <summary>Marks an accepted job as started (partner has arrived / begun work) - moves the booking to <see cref="Nestly.Domain.BookingStatus.InProgress"/>.</summary>
    Task<Result<PartnerJobDetailResponse>> StartAsync(Guid partnerId, Guid bookingId);

    /// <summary>
    /// Marks an in-progress job as completed - moves the booking to
    /// <see cref="Nestly.Domain.BookingStatus.Completed"/>. The resulting
    /// <c>BookingStatusChangedEvent</c> is what triggers escrow release and
    /// the partner's earning-ledger credit (task 148) via
    /// <c>EscrowReleaseOnCompletionHandler</c> - this method does not credit
    /// the ledger directly.
    /// </summary>
    Task<Result<PartnerJobDetailResponse>> CompleteAsync(Guid partnerId, Guid bookingId);

    Task<Result<PartnerJobDetailResponse>> UploadCompletionProofAsync(Guid partnerId, Guid bookingId, UploadJobCompletionProofRequest request);

    /// <summary>
    /// Submits (or resubmits) the richer completion evidence - photos plus
    /// checklist - that <see cref="CompleteAsync"/> requires to exist before
    /// it will move the booking to Completed (tasks 195-197). Distinct from
    /// <see cref="UploadCompletionProofAsync"/>'s single legacy proof-ref
    /// field: that one is supplementary evidence uploadable any time
    /// (including after completion); this one is the task 196 gate itself
    /// and must be submitted before <see cref="CompleteAsync"/> succeeds.
    /// </summary>
    Task<Result<BookingCompletionProofResponse>> SubmitCompletionProofAsync(Guid partnerId, Guid bookingId, SubmitCompletionProofRequest request);

    /// <summary>The completion proof this partner submitted for this job, if any (task 198's partner-side view).</summary>
    Task<Result<BookingCompletionProofResponse?>> GetCompletionProofAsync(Guid partnerId, Guid bookingId);
}

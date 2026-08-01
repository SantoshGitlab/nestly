using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.PartnerManagement;

/// <summary>
/// Admin-driven booking-to-partner assignment and rejection handling
/// (PARTNER.md "Assignment Bridge", tasks 147 and 159). Lives apart from
/// <c>IBookingManagementService</c> so that service's existing,
/// already-tested cancel/reschedule/refund flows are untouched - this
/// service only ever touches <see cref="Nestly.Domain.Booking.AssignedPartnerId"/>
/// (the one field PARTNER.md's SCOPE BOUNDARY allows) and the
/// <see cref="Nestly.Domain.BookingPartnerAssignment"/> bridge table.
/// </summary>
public interface IBookingPartnerAssignmentService
{
    /// <summary>
    /// Assigns (or re-assigns) a partner to a booking (task 147). Valid only
    /// while the booking is <see cref="Nestly.Domain.BookingStatus.AwaitingFulfilment"/>
    /// or already <see cref="Nestly.Domain.BookingStatus.Assigned"/> (a
    /// straight reassignment, which supersedes the current outstanding row
    /// via <see cref="Nestly.Domain.BookingPartnerAssignment.MarkReassigned"/>
    /// rather than leaving two live rows - PARTNER.md OPEN DECISIONS #5).
    /// </summary>
    Task<Result<BookingPartnerAssignmentResponse>> AssignAsync(Guid bookingId, Guid adminUserId, AssignPartnerRequest request);

    /// <summary>
    /// Rejects the booking's current outstanding assignment (task 159):
    /// clears <see cref="Nestly.Domain.Booking.AssignedPartnerId"/> and moves
    /// the booking back to <see cref="Nestly.Domain.BookingStatus.AwaitingFulfilment"/>
    /// so it re-enters the assignable pool - no automatic reassignment
    /// (PARTNER.md OPEN DECISIONS #1), an admin must call
    /// <see cref="AssignAsync"/> again.
    /// </summary>
    Task<Result<BookingPartnerAssignmentResponse>> RejectAsync(Guid bookingId, RejectAssignmentRequest request);

    /// <summary>
    /// The partner's own self-service acceptance of an outstanding
    /// assignment (task 149a, PARTNER.md API surface "accept job"). Verifies
    /// <paramref name="partnerId"/> actually owns the booking's currently
    /// outstanding assignment before calling <see cref="Nestly.Domain.BookingPartnerAssignment.Accept"/>
    /// (SRS 28.3 IDOR) - the caller's partner id must come from the JWT, not
    /// a route/body value.
    /// </summary>
    Task<Result<BookingPartnerAssignmentResponse>> AcceptAsync(Guid bookingId, Guid partnerId);

    /// <summary>
    /// The partner's own self-service rejection of an outstanding assignment
    /// (task 149a/159, PARTNER.md API surface "reject job") - the
    /// partner-authenticated counterpart to <see cref="RejectAsync"/>, which
    /// verifies <paramref name="partnerId"/> actually owns the outstanding
    /// assignment (SRS 28.3 IDOR) before applying the same reassignment-pool
    /// handling.
    /// </summary>
    Task<Result<BookingPartnerAssignmentResponse>> RejectByPartnerAsync(Guid bookingId, Guid partnerId, RejectAssignmentRequest request);

    /// <summary>Full assignment history for a booking, newest first (task 159 - lets the admin UI show why a booking needs reassignment).</summary>
    Task<Result<IReadOnlyList<BookingPartnerAssignmentResponse>>> GetHistoryAsync(Guid bookingId);
}

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

    /// <summary>Full assignment history for a booking, newest first (task 159 - lets the admin UI show why a booking needs reassignment).</summary>
    Task<Result<IReadOnlyList<BookingPartnerAssignmentResponse>>> GetHistoryAsync(Guid bookingId);
}

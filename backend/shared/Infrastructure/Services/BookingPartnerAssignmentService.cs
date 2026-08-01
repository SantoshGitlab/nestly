using Nestly.Application;
using Nestly.Application.Bookings;
using Nestly.Application.PartnerManagement;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;

namespace Nestly.Infrastructure.Services;

/// <inheritdoc cref="IBookingPartnerAssignmentService"/>
public class BookingPartnerAssignmentService : IBookingPartnerAssignmentService
{
    private readonly IBookingRepository _bookingRepository;
    private readonly IPartnerRepository _partnerRepository;
    private readonly IBookingPartnerAssignmentRepository _assignmentRepository;

    public BookingPartnerAssignmentService(
        IBookingRepository bookingRepository,
        IPartnerRepository partnerRepository,
        IBookingPartnerAssignmentRepository assignmentRepository)
    {
        _bookingRepository = bookingRepository;
        _partnerRepository = partnerRepository;
        _assignmentRepository = assignmentRepository;
    }

    public async Task<Result<BookingPartnerAssignmentResponse>> AssignAsync(Guid bookingId, Guid adminUserId, AssignPartnerRequest request)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId);
        if (booking is null)
        {
            return Error.NotFound("BookingPartnerAssignment.BookingNotFound", "Booking was not found.");
        }

        var partner = await _partnerRepository.GetByIdAsync(request.PartnerId);
        if (partner is null)
        {
            return Error.NotFound("BookingPartnerAssignment.PartnerNotFound", "Partner was not found.");
        }

        if (partner.Status != PartnerStatus.Active)
        {
            return Error.Business("BookingPartnerAssignment.PartnerNotActive", "Only an active partner can be assigned to a booking.");
        }

        if (booking.Status != BookingStatus.AwaitingFulfilment && booking.Status != BookingStatus.Assigned)
        {
            return Error.Business(
                "BookingPartnerAssignment.InvalidBookingStatus",
                $"A partner can only be assigned while the booking is AwaitingFulfilment or Assigned (current status: {booking.Status}).");
        }

        // Supersede whatever assignment is currently outstanding, if any -
        // PARTNER.md OPEN DECISIONS #5: only one row is ever "live" per booking.
        var currentAssignment = await _assignmentRepository.GetActiveByBookingAsync(bookingId);
        if (currentAssignment is not null)
        {
            currentAssignment.MarkReassigned();
            await _assignmentRepository.UpdateAsync(currentAssignment);
        }

        var assignment = new BookingPartnerAssignment(
            Guid.NewGuid(), bookingId, request.PartnerId, BookingAssignedByType.Admin, adminUserId, request.ResponseDeadline);
        await _assignmentRepository.AddAsync(assignment);

        if (booking.Status == BookingStatus.AwaitingFulfilment)
        {
            booking.TransitionTo(BookingStatus.Assigned, "Partner assigned by admin.");
        }

        booking.AssignPartner(request.PartnerId);
        await _bookingRepository.UpdateAsync(booking);

        return ToResponse(assignment, partner.DisplayName);
    }

    public async Task<Result<BookingPartnerAssignmentResponse>> RejectAsync(Guid bookingId, RejectAssignmentRequest request)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId);
        if (booking is null)
        {
            return Error.NotFound("BookingPartnerAssignment.BookingNotFound", "Booking was not found.");
        }

        var assignment = await _assignmentRepository.GetActiveByBookingAsync(bookingId);
        if (assignment is null || assignment.Status != BookingPartnerAssignmentStatus.Assigned)
        {
            return Error.Business("BookingPartnerAssignment.NoOutstandingAssignment", "This booking has no outstanding assignment to reject.");
        }

        assignment.Reject(request.Reason);
        await _assignmentRepository.UpdateAsync(assignment);

        // Needs reassignment (task 159): clear the display field and return
        // the booking to the assignable pool. No auto-match - PARTNER.md
        // OPEN DECISIONS #1 - an admin must call AssignAsync again.
        booking.AssignPartner(null);
        if (booking.Status == BookingStatus.Assigned)
        {
            booking.TransitionTo(BookingStatus.AwaitingFulfilment, "Partner rejected assignment; needs reassignment.");
        }

        await _bookingRepository.UpdateAsync(booking);

        var partner = await _partnerRepository.GetByIdAsync(assignment.PartnerId);
        return ToResponse(assignment, partner?.DisplayName ?? "(unknown partner)");
    }

    public async Task<Result<IReadOnlyList<BookingPartnerAssignmentResponse>>> GetHistoryAsync(Guid bookingId)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId);
        if (booking is null)
        {
            return Error.NotFound("BookingPartnerAssignment.BookingNotFound", "Booking was not found.");
        }

        var history = await _assignmentRepository.ListByBookingAsync(bookingId);
        var partnerCache = new Dictionary<Guid, string>();
        var items = new List<BookingPartnerAssignmentResponse>();
        foreach (var assignment in history)
        {
            if (!partnerCache.TryGetValue(assignment.PartnerId, out var displayName))
            {
                var partner = await _partnerRepository.GetByIdAsync(assignment.PartnerId);
                displayName = partner?.DisplayName ?? "(unknown partner)";
                partnerCache[assignment.PartnerId] = displayName;
            }

            items.Add(ToResponse(assignment, displayName));
        }

        return items;
    }

    private static BookingPartnerAssignmentResponse ToResponse(BookingPartnerAssignment assignment, string partnerDisplayName) => new(
        assignment.Id,
        assignment.BookingId,
        assignment.PartnerId,
        partnerDisplayName,
        assignment.AssignedByType,
        assignment.AssignedByUserId,
        assignment.AssignedAt,
        assignment.Status,
        assignment.ResponseDeadline,
        assignment.RespondedAt,
        assignment.Notes);
}

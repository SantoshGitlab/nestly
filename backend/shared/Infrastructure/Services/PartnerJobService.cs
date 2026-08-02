using Nestly.Application;
using Nestly.Application.Bookings;
using Nestly.Application.PartnerJobs;
using Nestly.Application.PartnerManagement;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;

namespace Nestly.Infrastructure.Services;

/// <inheritdoc cref="IPartnerJobService"/>
public class PartnerJobService : IPartnerJobService
{
    private readonly IBookingRepository _bookingRepository;
    private readonly IBookingPartnerAssignmentRepository _assignmentRepository;
    private readonly IBookingPartnerAssignmentService _assignmentService;
    private readonly IBookingCompletionProofRepository _completionProofRepository;

    public PartnerJobService(
        IBookingRepository bookingRepository,
        IBookingPartnerAssignmentRepository assignmentRepository,
        IBookingPartnerAssignmentService assignmentService,
        IBookingCompletionProofRepository completionProofRepository)
    {
        _bookingRepository = bookingRepository;
        _assignmentRepository = assignmentRepository;
        _assignmentService = assignmentService;
        _completionProofRepository = completionProofRepository;
    }

    public async Task<Result<PartnerJobSearchResponse>> ListAsync(Guid partnerId, PartnerJobStatus? status, DateOnly? date)
    {
        var assignments = await _assignmentRepository.ListByPartnerAsync(partnerId);

        var items = new List<PartnerJobSummaryResponse>();
        foreach (var assignment in assignments)
        {
            var booking = await _bookingRepository.GetByIdAsync(assignment.BookingId);
            if (booking is null)
            {
                continue;
            }

            var jobStatus = ToJobStatus(assignment, booking);
            if (status is not null && jobStatus != status)
            {
                continue;
            }

            if (date is not null && booking.SlotDate != date)
            {
                continue;
            }

            items.Add(new PartnerJobSummaryResponse(
                assignment.Id,
                booking.Id,
                jobStatus,
                booking.CustomerNameSnapshot,
                booking.CustomerMobileSnapshot,
                booking.AddressLine1Snapshot,
                booking.AddressCitySnapshot,
                booking.AddressPincodeSnapshot,
                booking.SlotDate,
                booking.SlotStartTimeSnapshot,
                booking.SlotEndTimeSnapshot,
                booking.TotalPayableSnapshot,
                assignment.AssignedAt,
                assignment.ResponseDeadline));
        }

        return new PartnerJobSearchResponse(items);
    }

    public async Task<Result<PartnerJobDetailResponse>> GetDetailAsync(Guid partnerId, Guid bookingId)
    {
        var resolved = await ResolveAsync(partnerId, bookingId);
        if (resolved is null)
        {
            return NotFoundError();
        }

        return ToDetailResponse(resolved.Value.Assignment, resolved.Value.Booking);
    }

    public async Task<Result<PartnerJobDetailResponse>> AcceptAsync(Guid partnerId, Guid bookingId)
    {
        var acceptResult = await _assignmentService.AcceptAsync(bookingId, partnerId);
        if (acceptResult.IsFailure)
        {
            return acceptResult.Error;
        }

        var resolved = await ResolveAsync(partnerId, bookingId);
        if (resolved is null)
        {
            return NotFoundError();
        }

        return ToDetailResponse(resolved.Value.Assignment, resolved.Value.Booking);
    }

    public async Task<Result<PartnerJobDetailResponse>> RejectAsync(Guid partnerId, Guid bookingId, RejectJobRequest request)
    {
        var rejectResult = await _assignmentService.RejectByPartnerAsync(bookingId, partnerId, new RejectAssignmentRequest(request.Reason));
        if (rejectResult.IsFailure)
        {
            return rejectResult.Error;
        }

        var booking = await _bookingRepository.GetByIdAsync(bookingId);
        if (booking is null)
        {
            return NotFoundError();
        }

        var assignment = await _assignmentRepository.GetByIdAsync(rejectResult.Value.Id);
        if (assignment is null)
        {
            return NotFoundError();
        }

        return ToDetailResponse(assignment, booking);
    }

    public async Task<Result<PartnerJobDetailResponse>> StartAsync(Guid partnerId, Guid bookingId)
    {
        var resolved = await ResolveAcceptedAsync(partnerId, bookingId);
        if (resolved is null)
        {
            return NotFoundError();
        }

        if (resolved.Value.Assignment.Status != BookingPartnerAssignmentStatus.Accepted)
        {
            return Error.Business("PartnerJob.NotAccepted", "The job must be accepted before it can be started.");
        }

        var (assignment, booking) = resolved.Value;
        try
        {
            booking.TransitionTo(BookingStatus.InProgress, "Partner started the job.");
        }
        catch (InvalidOperationException ex)
        {
            return Error.Business("PartnerJob.InvalidTransition", ex.Message);
        }

        await _bookingRepository.UpdateAsync(booking);

        return ToDetailResponse(assignment, booking);
    }

    public async Task<Result<PartnerJobDetailResponse>> CompleteAsync(Guid partnerId, Guid bookingId)
    {
        var resolved = await ResolveAcceptedAsync(partnerId, bookingId);
        if (resolved is null)
        {
            return NotFoundError();
        }

        var (assignment, booking) = resolved.Value;
        if (booking.Status != BookingStatus.InProgress)
        {
            return Error.Business("PartnerJob.NotStarted", "The job must be started before it can be marked completed.");
        }

        var proofError = await _completionProofRepository.EnsureCompletionProofExistsAsync(bookingId);
        if (proofError is not null)
        {
            return proofError;
        }

        try
        {
            // Raises BookingStatusChangedEvent -> EscrowReleaseOnCompletionHandler,
            // which releases escrow to this partner and credits their
            // earning ledger (task 148) once this save commits.
            booking.TransitionTo(BookingStatus.Completed, "Partner marked the job completed.");
        }
        catch (InvalidOperationException ex)
        {
            return Error.Business("PartnerJob.InvalidTransition", ex.Message);
        }

        await _bookingRepository.UpdateAsync(booking);

        return ToDetailResponse(assignment, booking);
    }

    public async Task<Result<PartnerJobDetailResponse>> UploadCompletionProofAsync(Guid partnerId, Guid bookingId, UploadJobCompletionProofRequest request)
    {
        var resolved = await ResolveAcceptedAsync(partnerId, bookingId);
        if (resolved is null)
        {
            return NotFoundError();
        }

        var (assignment, booking) = resolved.Value;
        try
        {
            assignment.SetCompletionProof(request.ProofRef);
        }
        catch (InvalidOperationException ex)
        {
            return Error.Business("PartnerJob.InvalidTransition", ex.Message);
        }

        await _assignmentRepository.UpdateAsync(assignment);

        return ToDetailResponse(assignment, booking);
    }

    public async Task<Result<BookingCompletionProofResponse>> SubmitCompletionProofAsync(Guid partnerId, Guid bookingId, SubmitCompletionProofRequest request)
    {
        var resolved = await ResolveAcceptedAsync(partnerId, bookingId);
        if (resolved is null)
        {
            return NotFoundError();
        }

        var checklistAnswers = request.ChecklistAnswers
            .Select(a => new CompletionChecklistAnswer(a.Item, a.Completed, a.Notes))
            .ToList();

        var existing = await _completionProofRepository.GetByBookingIdAsync(bookingId);
        if (existing is null)
        {
            var proof = new BookingCompletionProof(Guid.NewGuid(), bookingId, partnerId, request.PhotoRefs, checklistAnswers);
            await _completionProofRepository.AddAsync(proof);
            return proof.ToResponse()!;
        }

        existing.Update(request.PhotoRefs, checklistAnswers);
        await _completionProofRepository.UpdateAsync(existing);
        return existing.ToResponse()!;
    }

    public async Task<Result<BookingCompletionProofResponse?>> GetCompletionProofAsync(Guid partnerId, Guid bookingId)
    {
        var resolved = await ResolveAcceptedAsync(partnerId, bookingId);
        if (resolved is null)
        {
            return NotFoundError();
        }

        var proof = await _completionProofRepository.GetByBookingIdAsync(bookingId);
        return proof.ToResponse();
    }

    /// <summary>Resolves the most recent assignment this partner ever had on this booking, alongside the booking itself. Null if the booking doesn't exist or was never assigned to this partner (SRS 28.3 IDOR - the caller maps this to a 404, hiding the booking's existence from a non-owning partner).</summary>
    private async Task<(BookingPartnerAssignment Assignment, Booking Booking)?> ResolveAsync(Guid partnerId, Guid bookingId)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId);
        if (booking is null)
        {
            return null;
        }

        var history = await _assignmentRepository.ListByBookingAsync(bookingId);
        var assignment = history.Where(a => a.PartnerId == partnerId).MaxBy(a => a.AssignedAt);
        if (assignment is null)
        {
            return null;
        }

        return (assignment, booking);
    }

    /// <summary>Same as <see cref="ResolveAsync"/> but only returns a result when the resolved assignment is this partner's currently <see cref="BookingPartnerAssignmentStatus.Accepted"/> one - the precondition for start/complete/proof-upload.</summary>
    private async Task<(BookingPartnerAssignment Assignment, Booking Booking)?> ResolveAcceptedAsync(Guid partnerId, Guid bookingId)
    {
        var resolved = await ResolveAsync(partnerId, bookingId);
        if (resolved is null || resolved.Value.Assignment.Status != BookingPartnerAssignmentStatus.Accepted)
        {
            return null;
        }

        return resolved;
    }

    private static Error NotFoundError() =>
        Error.NotFound("PartnerJob.NotFound", "No job was found for this booking.");

    private static PartnerJobStatus ToJobStatus(BookingPartnerAssignment assignment, Booking booking) => assignment.Status switch
    {
        BookingPartnerAssignmentStatus.Assigned => PartnerJobStatus.Assigned,
        BookingPartnerAssignmentStatus.Rejected => PartnerJobStatus.Rejected,
        BookingPartnerAssignmentStatus.Reassigned => PartnerJobStatus.Reassigned,
        BookingPartnerAssignmentStatus.Withdrawn => PartnerJobStatus.Withdrawn,
        BookingPartnerAssignmentStatus.Accepted => booking.Status switch
        {
            BookingStatus.Completed => PartnerJobStatus.Completed,
            BookingStatus.InProgress => PartnerJobStatus.InProgress,
            _ => PartnerJobStatus.Accepted
        },
        _ => PartnerJobStatus.Assigned
    };

    private static PartnerJobDetailResponse ToDetailResponse(BookingPartnerAssignment assignment, Booking booking) => new(
        assignment.Id,
        booking.Id,
        ToJobStatus(assignment, booking),
        booking.CustomerNameSnapshot,
        booking.CustomerMobileSnapshot,
        booking.AddressLabelSnapshot,
        booking.AddressLine1Snapshot,
        booking.AddressLine2Snapshot,
        booking.AddressLandmarkSnapshot,
        booking.AddressCitySnapshot,
        booking.AddressStateSnapshot,
        booking.AddressPincodeSnapshot,
        booking.AddressContactNameSnapshot,
        booking.AddressContactMobileSnapshot,
        booking.SlotDate,
        booking.SlotStartTimeSnapshot,
        booking.SlotEndTimeSnapshot,
        booking.Items.Select(i => new PartnerJobItemResponse(i.NameSnapshot, i.Quantity, i.UnitPriceSnapshot)).ToList(),
        booking.TotalPayableSnapshot,
        assignment.AssignedAt,
        assignment.RespondedAt,
        assignment.ResponseDeadline,
        assignment.Notes,
        assignment.CompletionProofRef);
}

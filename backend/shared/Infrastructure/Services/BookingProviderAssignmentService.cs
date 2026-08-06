using Microsoft.EntityFrameworkCore;
using Nestly.Application;
using Nestly.Application.Bookings;
using Nestly.Application.ProviderManagement;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence;

namespace Nestly.Infrastructure.Services;

/// <inheritdoc cref="IBookingProviderAssignmentService"/>
public class BookingProviderAssignmentService : IBookingProviderAssignmentService
{
    private readonly IBookingRepository _bookingRepository;
    private readonly IProviderRepository _providerRepository;
    private readonly IServiceRepository _serviceRepository;
    private readonly IBookingProviderAssignmentRepository _assignmentRepository;
    // Matching candidates for GetEligibleProvidersAsync spans Provider,
    // ProviderServiceArea, ProviderSkillMapping, ProviderCapacity and Pincode
    // in one read - no single existing repository owns that join, so this
    // reads directly off the shared context, same as RefundService/
    // PaymentWebhookService do for their own cross-aggregate needs.
    private readonly NestlyDbContext _context;

    public BookingProviderAssignmentService(
        IBookingRepository bookingRepository,
        IProviderRepository providerRepository,
        IServiceRepository serviceRepository,
        IBookingProviderAssignmentRepository assignmentRepository,
        NestlyDbContext context)
    {
        _bookingRepository = bookingRepository;
        _providerRepository = providerRepository;
        _serviceRepository = serviceRepository;
        _assignmentRepository = assignmentRepository;
        _context = context;
    }

    public Task<Result<BookingProviderAssignmentResponse>> AssignAsync(Guid bookingId, Guid adminUserId, AssignProviderRequest request) =>
        AssignInternalAsync(
            bookingId, request.ProviderId, BookingAssignedByType.Admin, adminUserId, request.ResponseDeadline, "Provider assigned by admin.");

    /// <summary>
    /// Phase 14 (task 246): the automatic-assignment engine's counterpart to
    /// <see cref="AssignAsync"/> - same validation and PROVIDER.md OPEN
    /// DECISIONS #5 supersede-the-outstanding-row behaviour, but records
    /// <see cref="BookingAssignedByType.System"/> with no acting admin id,
    /// exactly what that column has existed to support since task 147
    /// (PROVIDER.md OPEN DECISIONS #1: "the bridge table's assigned_by
    /// column already distinguishes admin from system, so an automatic
    /// assignment engine can be added later purely as a new writer of that
    /// same table" - this is that later writer). No response deadline: task
    /// 247's rejection-retry chain moves on to the next candidate itself
    /// rather than waiting one out.
    /// </summary>
    public Task<Result<BookingProviderAssignmentResponse>> AssignBySystemAsync(Guid bookingId, Guid providerId) =>
        AssignInternalAsync(bookingId, providerId, BookingAssignedByType.System, adminUserId: null, responseDeadline: null, "Provider auto-assigned by the matching engine.");

    private async Task<Result<BookingProviderAssignmentResponse>> AssignInternalAsync(
        Guid bookingId, Guid providerId, BookingAssignedByType assignedByType, Guid? adminUserId, DateTime? responseDeadline, string reason)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId);
        if (booking is null)
        {
            return Error.NotFound("BookingProviderAssignment.BookingNotFound", "Booking was not found.");
        }

        var provider = await _providerRepository.GetByIdAsync(providerId);
        if (provider is null)
        {
            return Error.NotFound("BookingProviderAssignment.ProviderNotFound", "Provider was not found.");
        }

        if (provider.Status != ProviderStatus.Active)
        {
            return Error.Business("BookingProviderAssignment.ProviderNotActive", "Only an active provider can be assigned to a booking.");
        }

        if (booking.Status != BookingStatus.AwaitingFulfilment && booking.Status != BookingStatus.Assigned)
        {
            return Error.Business(
                "BookingProviderAssignment.InvalidBookingStatus",
                $"A provider can only be assigned while the booking is AwaitingFulfilment or Assigned (current status: {booking.Status}).");
        }

        // Supersede whatever assignment is currently outstanding, if any -
        // PROVIDER.md OPEN DECISIONS #5: only one row is ever "live" per booking.
        var currentAssignment = await _assignmentRepository.GetActiveByBookingAsync(bookingId);
        if (currentAssignment is not null)
        {
            currentAssignment.MarkReassigned();
            await _assignmentRepository.UpdateAsync(currentAssignment);
        }

        var assignment = new BookingProviderAssignment(
            Guid.NewGuid(), bookingId, providerId, assignedByType, adminUserId, responseDeadline);
        await _assignmentRepository.AddAsync(assignment);

        if (booking.Status == BookingStatus.AwaitingFulfilment)
        {
            booking.TransitionTo(BookingStatus.Assigned, reason);
        }

        booking.AssignProvider(providerId);
        await _bookingRepository.UpdateAsync(booking);

        return ToResponse(assignment, provider.DisplayName);
    }

    public async Task<Result<BookingProviderAssignmentResponse>> RejectAsync(Guid bookingId, RejectAssignmentRequest request)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId);
        if (booking is null)
        {
            return Error.NotFound("BookingProviderAssignment.BookingNotFound", "Booking was not found.");
        }

        var assignment = await _assignmentRepository.GetActiveByBookingAsync(bookingId);
        if (assignment is null || assignment.Status != BookingProviderAssignmentStatus.Assigned)
        {
            return Error.Business("BookingProviderAssignment.NoOutstandingAssignment", "This booking has no outstanding assignment to reject.");
        }

        return await RejectInternalAsync(booking, assignment, request.Reason);
    }

    public async Task<Result<BookingProviderAssignmentResponse>> AcceptAsync(Guid bookingId, Guid providerId)
    {
        var assignment = await _assignmentRepository.GetActiveByBookingAsync(bookingId);
        if (assignment is null || assignment.ProviderId != providerId)
        {
            // Hides whether the booking/assignment exists at all from a
            // non-owning caller (SRS 28.3 IDOR), same pattern as
            // ProviderAvailabilityService.DeleteBlackoutDateAsync.
            return Error.NotFound("BookingProviderAssignment.NoOutstandingAssignment", "You have no outstanding assignment for this booking.");
        }

        if (assignment.Status != BookingProviderAssignmentStatus.Assigned)
        {
            return Error.Business("BookingProviderAssignment.AlreadyResponded", $"This assignment was already {assignment.Status}.");
        }

        assignment.Accept();
        await _assignmentRepository.UpdateAsync(assignment);

        var provider = await _providerRepository.GetByIdAsync(providerId);
        return ToResponse(assignment, provider?.DisplayName ?? "(unknown provider)");
    }

    public async Task<Result<BookingProviderAssignmentResponse>> RejectByProviderAsync(Guid bookingId, Guid providerId, RejectAssignmentRequest request)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId);
        if (booking is null)
        {
            return Error.NotFound("BookingProviderAssignment.BookingNotFound", "Booking was not found.");
        }

        var assignment = await _assignmentRepository.GetActiveByBookingAsync(bookingId);
        if (assignment is null || assignment.ProviderId != providerId)
        {
            return Error.NotFound("BookingProviderAssignment.NoOutstandingAssignment", "You have no outstanding assignment for this booking.");
        }

        if (assignment.Status != BookingProviderAssignmentStatus.Assigned)
        {
            return Error.Business("BookingProviderAssignment.AlreadyResponded", $"This assignment was already {assignment.Status}.");
        }

        return await RejectInternalAsync(booking, assignment, request.Reason);
    }

    /// <summary>
    /// Shared task 159 handling behind both the admin-recorded
    /// <see cref="RejectAsync"/> and the provider-authenticated
    /// <see cref="RejectByProviderAsync"/>: reject the assignment, clear the
    /// display field, and return the booking to the assignable pool.
    /// </summary>
    private async Task<Result<BookingProviderAssignmentResponse>> RejectInternalAsync(Booking booking, BookingProviderAssignment assignment, string? reason)
    {
        assignment.Reject(reason);
        await _assignmentRepository.UpdateAsync(assignment);

        // Needs reassignment (task 159): clear the display field and return
        // the booking to the assignable pool. No auto-match - PROVIDER.md
        // OPEN DECISIONS #1 - an admin must call AssignAsync again.
        booking.AssignProvider(null);
        if (booking.Status == BookingStatus.Assigned)
        {
            booking.TransitionTo(BookingStatus.AwaitingFulfilment, "Provider rejected assignment; needs reassignment.");
        }

        await _bookingRepository.UpdateAsync(booking);

        var provider = await _providerRepository.GetByIdAsync(assignment.ProviderId);
        return ToResponse(assignment, provider?.DisplayName ?? "(unknown provider)");
    }

    public async Task<Result<IReadOnlyList<BookingProviderAssignmentResponse>>> GetHistoryAsync(Guid bookingId)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId);
        if (booking is null)
        {
            return Error.NotFound("BookingProviderAssignment.BookingNotFound", "Booking was not found.");
        }

        var history = await _assignmentRepository.ListByBookingAsync(bookingId);

        // Task 257: the local dictionary only saved a repeat lookup for a
        // provider already seen in this history - the first assignment for
        // each distinct provider still cost its own round trip.
        var displayNames = await _providerRepository.GetDisplayNamesByIdsAsync(
            history.Select(a => a.ProviderId).Distinct().ToList());

        return history
            .Select(assignment => ToResponse(assignment, displayNames.GetValueOrDefault(assignment.ProviderId, "(unknown provider)")))
            .ToList();
    }

    public async Task<Result<IReadOnlyList<EligibleProviderResponse>>> GetEligibleProvidersAsync(Guid bookingId)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId);
        if (booking is null)
        {
            return Error.NotFound("BookingProviderAssignment.BookingNotFound", "Booking was not found.");
        }

        if (booking.Items.Count == 0)
        {
            return Error.Business("BookingProviderAssignment.NoServiceOnBooking", "This booking has no service line item to match providers against.");
        }

        var serviceId = booking.Items[0].ServiceId;
        var service = await _serviceRepository.GetByIdAsync(serviceId);
        if (service is null)
        {
            return Error.NotFound("BookingProviderAssignment.ServiceNotFound", "The booking's service could not be found.");
        }

        // The booking only ever stores the pincode as a snapshot string (see
        // Booking's doc comment on why every address field is a snapshot,
        // not a live FK) - resolve it back to a real Pincode row so it can be
        // compared against ProviderServiceArea's actual foreign keys.
        var pincode = await _context.Set<Pincode>()
            .FirstOrDefaultAsync(p => p.Code == booking.AddressPincodeSnapshot);
        if (pincode is null)
        {
            return Error.NotFound(
                "BookingProviderAssignment.PincodeNotFound",
                "The booking's address pincode is not a recognised serviceable pincode.");
        }

        // A provider matches if their declared service area covers this
        // pincode specifically (PincodeId set) or the whole city
        // (PincodeId null), and their declared skill covers this service
        // specifically (ServiceId set) or the whole category (ServiceId
        // null) - the same "narrows an optional broader default" shape
        // ProviderServiceArea/ProviderSkillMapping's own doc comments
        // describe, not a distance calculation (this schema has no provider
        // coordinates - see PROVIDER.md OPEN DECISIONS #1's "distance...
        // doesn't exist yet").
        var candidateRows = await (
            from provider in _context.Set<Provider>()
            where provider.Status == ProviderStatus.Active
            join area in _context.Set<ProviderServiceArea>() on provider.Id equals area.ProviderId
            join skill in _context.Set<ProviderSkillMapping>() on provider.Id equals skill.ProviderId
            where area.IsActive && area.CityId == pincode.CityId
                && (area.PincodeId == null || area.PincodeId == pincode.Id)
            where skill.IsActive && skill.CategoryId == service.CategoryId
                && (skill.ServiceId == null || skill.ServiceId == serviceId)
            select new
            {
                ProviderId = provider.Id,
                provider.DisplayName,
                provider.Phone,
                PincodeMatch = area.PincodeId != null,
                ServiceMatch = skill.ServiceId != null,
            }
        ).ToListAsync();

        if (candidateRows.Count == 0)
        {
            return Array.Empty<EligibleProviderResponse>();
        }

        // A provider can satisfy the join above through more than one
        // area/skill row (e.g. both a city-wide and a pincode-specific
        // area) - collapse to one row per provider, keeping the most
        // specific match found on either dimension.
        var bestPerProvider = candidateRows
            .GroupBy(x => x.ProviderId)
            .Select(g => new
            {
                ProviderId = g.Key,
                DisplayName = g.First().DisplayName,
                Phone = g.First().Phone,
                PincodeMatch = g.Any(x => x.PincodeMatch),
                ServiceMatch = g.Any(x => x.ServiceMatch),
            })
            .ToList();

        var providerIds = bestPerProvider.Select(x => x.ProviderId).ToList();

        var capacityByProvider = await _context.Set<ProviderCapacity>()
            .Where(c => providerIds.Contains(c.ProviderId))
            .ToDictionaryAsync(c => c.ProviderId, c => c.MaxJobsPerDay);

        // Advisory load signal (ProviderCapacity's own doc comment: "an admin
        // can consult them when hand-assigning a booking") - counts this
        // provider's other live jobs on the same slot date, not a hard filter.
        var jobsTodayByProvider = await _context.Set<Booking>()
            .Where(b => b.AssignedProviderId != null
                && providerIds.Contains(b.AssignedProviderId!.Value)
                && b.SlotDate == booking.SlotDate
                && (b.Status == BookingStatus.Assigned || b.Status == BookingStatus.InProgress))
            .GroupBy(b => b.AssignedProviderId!.Value)
            .Select(g => new { ProviderId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ProviderId, x => x.Count);

        var results = bestPerProvider
            .Select(x => new EligibleProviderResponse(
                x.ProviderId,
                x.DisplayName,
                x.Phone,
                x.PincodeMatch,
                x.ServiceMatch,
                capacityByProvider.GetValueOrDefault(x.ProviderId),
                jobsTodayByProvider.GetValueOrDefault(x.ProviderId)))
            // Most specific match first (exact pincode, exact service), then
            // least-loaded today - never by rating (OPEN DECISIONS #4).
            .OrderByDescending(r => r.PincodeMatch)
            .ThenByDescending(r => r.ServiceMatch)
            .ThenBy(r => r.AssignedJobsToday)
            .ToList();

        return results;
    }

    private static BookingProviderAssignmentResponse ToResponse(BookingProviderAssignment assignment, string providerDisplayName) => new(
        assignment.Id,
        assignment.BookingId,
        assignment.ProviderId,
        providerDisplayName,
        assignment.AssignedByType,
        assignment.AssignedByUserId,
        assignment.AssignedAt,
        assignment.Status,
        assignment.ResponseDeadline,
        assignment.RespondedAt,
        assignment.Notes,
        assignment.CompletionProofRef);
}

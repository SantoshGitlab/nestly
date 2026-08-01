using Nestly.Application;
using Nestly.Application.Bookings;
using Nestly.Application.PartnerManagement;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;

namespace Nestly.Infrastructure.Services;

/// <inheritdoc cref="IPartnerManagementService"/>
public class PartnerManagementService : IPartnerManagementService
{
    private readonly IPartnerRepository _partnerRepository;
    private readonly IPartnerKycDocumentRepository _kycDocumentRepository;
    private readonly IPartnerBackgroundCheckRepository _backgroundCheckRepository;
    private readonly IBookingRepository _bookingRepository;
    private readonly IBookingPartnerAssignmentRepository _assignmentRepository;
    private readonly IPartnerEarningLedgerRepository _earningLedgerRepository;

    public PartnerManagementService(
        IPartnerRepository partnerRepository,
        IPartnerKycDocumentRepository kycDocumentRepository,
        IPartnerBackgroundCheckRepository backgroundCheckRepository,
        IBookingRepository bookingRepository,
        IBookingPartnerAssignmentRepository assignmentRepository,
        IPartnerEarningLedgerRepository earningLedgerRepository)
    {
        _partnerRepository = partnerRepository;
        _kycDocumentRepository = kycDocumentRepository;
        _backgroundCheckRepository = backgroundCheckRepository;
        _bookingRepository = bookingRepository;
        _assignmentRepository = assignmentRepository;
        _earningLedgerRepository = earningLedgerRepository;
    }

    public async Task<Result<PartnerSearchResponse>> SearchAsync(PartnerSearchRequest request)
    {
        var filter = new PartnerSearchFilter(request.Name, request.Phone, request.Status, request.OnboardingStatus, request.Page, request.PageSize);
        var result = await _partnerRepository.SearchAsync(filter);

        var items = result.Rows.Select(p => new PartnerSummaryResponse(
            p.Id, p.LegalName, p.DisplayName, p.Phone, p.Email, p.Status, p.OnboardingStatus, p.CreatedAt)).ToList();

        return new PartnerSearchResponse(items, result.TotalCount, request.Page, request.PageSize);
    }

    public async Task<Result<PartnerDetailResponse>> GetDetailAsync(Guid partnerId)
    {
        var partner = await _partnerRepository.GetByIdAsync(partnerId);
        if (partner is null)
        {
            return Error.NotFound("Partner.NotFound", "Partner was not found.");
        }

        return await BuildDetailAsync(partner);
    }

    public async Task<Result<PartnerDetailResponse>> CreateAsync(CreatePartnerRequest request)
    {
        if (await _partnerRepository.ExistsByPhoneAsync(request.Phone))
        {
            return Error.Conflict("Partner.PhoneAlreadyExists", "A partner with this phone number already exists.");
        }

        Partner partner;
        try
        {
            partner = new Partner(Guid.NewGuid(), request.LegalName, request.DisplayName, PartnerType.Individual, request.Phone, request.Email);
        }
        catch (ArgumentException ex)
        {
            return Error.Validation("Partner.InvalidProfile", ex.Message);
        }

        await _partnerRepository.AddAsync(partner);

        return await BuildDetailAsync(partner);
    }

    public async Task<Result<PartnerDetailResponse>> UpdateAsync(Guid partnerId, UpdatePartnerRequest request)
    {
        var partner = await _partnerRepository.GetByIdAsync(partnerId);
        if (partner is null)
        {
            return Error.NotFound("Partner.NotFound", "Partner was not found.");
        }

        try
        {
            partner.UpdateProfile(request.LegalName, request.DisplayName, request.Email);
        }
        catch (ArgumentException ex)
        {
            return Error.Validation("Partner.InvalidProfile", ex.Message);
        }

        await _partnerRepository.UpdateAsync(partner);

        return await BuildDetailAsync(partner);
    }

    public async Task<Result<PartnerDetailResponse>> SuspendAsync(Guid partnerId, SuspendPartnerRequest request)
    {
        var partner = await _partnerRepository.GetByIdAsync(partnerId);
        if (partner is null)
        {
            return Error.NotFound("Partner.NotFound", "Partner was not found.");
        }

        if (partner.Status == PartnerStatus.Suspended)
        {
            return Error.Business("Partner.AlreadySuspended", "This partner is already suspended.");
        }

        partner.ChangeStatus(PartnerStatus.Suspended);
        await _partnerRepository.UpdateAsync(partner);

        return await BuildDetailAsync(partner);
    }

    public async Task<Result<PartnerDetailResponse>> ReactivateAsync(Guid partnerId)
    {
        var partner = await _partnerRepository.GetByIdAsync(partnerId);
        if (partner is null)
        {
            return Error.NotFound("Partner.NotFound", "Partner was not found.");
        }

        if (partner.Status != PartnerStatus.Suspended)
        {
            return Error.Business("Partner.NotSuspended", "Only a suspended partner can be reactivated.");
        }

        partner.ChangeStatus(PartnerStatus.Active);
        await _partnerRepository.UpdateAsync(partner);

        return await BuildDetailAsync(partner);
    }

    public async Task<Result<PartnerPerformanceResponse>> GetPerformanceAsync(Guid partnerId)
    {
        if (!await _partnerRepository.ExistsAsync(partnerId))
        {
            return Error.NotFound("Partner.NotFound", "Partner was not found.");
        }

        var assignments = await AssignmentsForPartnerAsync(partnerId);
        var bookings = await _bookingRepository.ListByAssignedPartnerAsync(partnerId);
        var latestLedgerEntry = await _earningLedgerRepository.GetLatestAsync(partnerId);

        return new PartnerPerformanceResponse(
            partnerId,
            TotalAssignments: assignments.Count,
            AcceptedAssignments: assignments.Count(a => a.Status == BookingPartnerAssignmentStatus.Accepted),
            RejectedAssignments: assignments.Count(a => a.Status == BookingPartnerAssignmentStatus.Rejected),
            CompletedJobs: bookings.Count(b => b.Status == BookingStatus.Completed),
            InProgressJobs: bookings.Count(b => b.Status == BookingStatus.InProgress),
            LifetimeEarnings: latestLedgerEntry?.BalanceAfter ?? 0m);
    }

    /// <summary>
    /// Every assignment ever made across every booking, filtered to this
    /// partner - there is no direct "list by partner" repository method
    /// (assignments are looked up per-booking elsewhere), so this composes
    /// per-booking history for the bookings currently or ever assigned to
    /// this partner. Good enough for a performance summary at today's data
    /// volume; a dedicated ListByPartnerAsync would be the next step if this
    /// ever needs to scale past an admin-facing detail page.
    /// </summary>
    private async Task<IReadOnlyList<BookingPartnerAssignment>> AssignmentsForPartnerAsync(Guid partnerId)
    {
        var bookings = await _bookingRepository.ListByAssignedPartnerAsync(partnerId);
        var assignments = new List<BookingPartnerAssignment>();
        foreach (var booking in bookings)
        {
            var history = await _assignmentRepository.ListByBookingAsync(booking.Id);
            assignments.AddRange(history.Where(a => a.PartnerId == partnerId));
        }

        return assignments;
    }

    private async Task<PartnerDetailResponse> BuildDetailAsync(Partner partner)
    {
        var documents = await _kycDocumentRepository.GetByPartnerAsync(partner.Id);
        var backgroundChecks = await _backgroundCheckRepository.ListByPartnerAsync(partner.Id);

        return PartnerDetailMapper.ToDetailResponse(partner, documents, backgroundChecks);
    }
}

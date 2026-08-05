using Nestly.Application;
using Nestly.Application.Bookings;
using Nestly.Application.ProviderManagement;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;

namespace Nestly.Infrastructure.Services;

/// <inheritdoc cref="IProviderManagementService"/>
public class ProviderManagementService : IProviderManagementService
{
    private readonly IProviderRepository _providerRepository;
    private readonly IProviderKycDocumentRepository _kycDocumentRepository;
    private readonly IProviderBackgroundCheckRepository _backgroundCheckRepository;
    private readonly IBookingRepository _bookingRepository;
    private readonly IBookingProviderAssignmentRepository _assignmentRepository;
    private readonly IProviderEarningLedgerRepository _earningLedgerRepository;

    public ProviderManagementService(
        IProviderRepository providerRepository,
        IProviderKycDocumentRepository kycDocumentRepository,
        IProviderBackgroundCheckRepository backgroundCheckRepository,
        IBookingRepository bookingRepository,
        IBookingProviderAssignmentRepository assignmentRepository,
        IProviderEarningLedgerRepository earningLedgerRepository)
    {
        _providerRepository = providerRepository;
        _kycDocumentRepository = kycDocumentRepository;
        _backgroundCheckRepository = backgroundCheckRepository;
        _bookingRepository = bookingRepository;
        _assignmentRepository = assignmentRepository;
        _earningLedgerRepository = earningLedgerRepository;
    }

    public async Task<Result<ProviderSearchResponse>> SearchAsync(ProviderSearchRequest request)
    {
        var filter = new ProviderSearchFilter(request.Name, request.Phone, request.Status, request.OnboardingStatus, request.Page, request.PageSize);
        var result = await _providerRepository.SearchAsync(filter);

        var items = result.Rows.Select(p => new ProviderSummaryResponse(
            p.Id, p.LegalName, p.DisplayName, p.Phone, p.Email, p.Status, p.OnboardingStatus, p.CreatedAt)).ToList();

        return new ProviderSearchResponse(items, result.TotalCount, request.Page, request.PageSize);
    }

    public async Task<Result<ProviderDetailResponse>> GetDetailAsync(Guid providerId)
    {
        var provider = await _providerRepository.GetByIdAsync(providerId);
        if (provider is null)
        {
            return Error.NotFound("Provider.NotFound", "Provider was not found.");
        }

        return await BuildDetailAsync(provider);
    }

    public async Task<Result<ProviderDetailResponse>> CreateAsync(CreateProviderRequest request)
    {
        if (await _providerRepository.ExistsByPhoneAsync(request.Phone))
        {
            return Error.Conflict("Provider.PhoneAlreadyExists", "A provider with this phone number already exists.");
        }

        Provider provider;
        try
        {
            provider = new Provider(Guid.NewGuid(), request.LegalName, request.DisplayName, ProviderType.Individual, request.Phone, request.Email);
        }
        catch (ArgumentException ex)
        {
            return Error.Validation("Provider.InvalidProfile", ex.Message);
        }

        await _providerRepository.AddAsync(provider);

        return await BuildDetailAsync(provider);
    }

    public async Task<Result<ProviderDetailResponse>> UpdateAsync(Guid providerId, UpdateProviderRequest request)
    {
        var provider = await _providerRepository.GetByIdAsync(providerId);
        if (provider is null)
        {
            return Error.NotFound("Provider.NotFound", "Provider was not found.");
        }

        try
        {
            provider.UpdateProfile(request.LegalName, request.DisplayName, request.Email);
            provider.UpdateLocation(request.Latitude, request.Longitude);
        }
        catch (ArgumentException ex)
        {
            return Error.Validation("Provider.InvalidProfile", ex.Message);
        }

        await _providerRepository.UpdateAsync(provider);

        return await BuildDetailAsync(provider);
    }

    public async Task<Result<ProviderDetailResponse>> SuspendAsync(Guid providerId, SuspendProviderRequest request)
    {
        var provider = await _providerRepository.GetByIdAsync(providerId);
        if (provider is null)
        {
            return Error.NotFound("Provider.NotFound", "Provider was not found.");
        }

        if (provider.Status == ProviderStatus.Suspended)
        {
            return Error.Business("Provider.AlreadySuspended", "This provider is already suspended.");
        }

        provider.ChangeStatus(ProviderStatus.Suspended);
        await _providerRepository.UpdateAsync(provider);

        return await BuildDetailAsync(provider);
    }

    public async Task<Result<ProviderDetailResponse>> ReactivateAsync(Guid providerId)
    {
        var provider = await _providerRepository.GetByIdAsync(providerId);
        if (provider is null)
        {
            return Error.NotFound("Provider.NotFound", "Provider was not found.");
        }

        if (provider.Status != ProviderStatus.Suspended)
        {
            return Error.Business("Provider.NotSuspended", "Only a suspended provider can be reactivated.");
        }

        provider.ChangeStatus(ProviderStatus.Active);
        await _providerRepository.UpdateAsync(provider);

        return await BuildDetailAsync(provider);
    }

    public async Task<Result<ProviderPerformanceResponse>> GetPerformanceAsync(Guid providerId)
    {
        if (!await _providerRepository.ExistsAsync(providerId))
        {
            return Error.NotFound("Provider.NotFound", "Provider was not found.");
        }

        var assignments = await AssignmentsForProviderAsync(providerId);
        var bookings = await _bookingRepository.ListByAssignedProviderAsync(providerId);
        var latestLedgerEntry = await _earningLedgerRepository.GetLatestAsync(providerId);

        return new ProviderPerformanceResponse(
            providerId,
            TotalAssignments: assignments.Count,
            AcceptedAssignments: assignments.Count(a => a.Status == BookingProviderAssignmentStatus.Accepted),
            RejectedAssignments: assignments.Count(a => a.Status == BookingProviderAssignmentStatus.Rejected),
            CompletedJobs: bookings.Count(b => b.Status == BookingStatus.Completed),
            InProgressJobs: bookings.Count(b => b.Status == BookingStatus.InProgress),
            LifetimeEarnings: latestLedgerEntry?.BalanceAfter ?? 0m);
    }

    /// <summary>
    /// Every assignment ever made to this provider, across every booking
    /// (tasks 258, 263).
    ///
    /// This used to start from <c>IBookingRepository.ListByAssignedProviderAsync</c>
    /// and pull each of those bookings' assignment history. That filters on
    /// <c>Booking.AssignedProviderId</c> - the live assignment only - so a
    /// booking this provider rejected, which is then reassigned to someone
    /// else, no longer pointed here and its Rejected row was never counted.
    /// Rejection is exactly the case that gets reassigned away, so
    /// RejectedAssignments reported near-zero however often the provider
    /// actually declined. It was also one query per booking.
    ///
    /// <c>ListByProviderAsync</c> answers the question directly, in one
    /// query, and keeps the rejected/superseded rows the metric is about.
    /// </summary>
    private Task<IReadOnlyList<BookingProviderAssignment>> AssignmentsForProviderAsync(Guid providerId) =>
        _assignmentRepository.ListByProviderAsync(providerId);

    private async Task<ProviderDetailResponse> BuildDetailAsync(Provider provider)
    {
        var documents = await _kycDocumentRepository.GetByProviderAsync(provider.Id);
        var backgroundChecks = await _backgroundCheckRepository.ListByProviderAsync(provider.Id);

        return ProviderDetailMapper.ToDetailResponse(provider, documents, backgroundChecks);
    }
}

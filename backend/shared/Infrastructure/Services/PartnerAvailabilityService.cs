using Nestly.Application;
using Nestly.Application.PartnerAvailability;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// CRUD over a partner's own recurring availability windows and blackout
/// dates (task 149b, PARTNER.md API surface "Availability").
/// </summary>
public class PartnerAvailabilityService : IPartnerAvailabilityService
{
    private readonly IPartnerRepository _partnerRepository;
    private readonly IPartnerAvailabilityWindowRepository _windowRepository;
    private readonly IPartnerBlackoutDateRepository _blackoutDateRepository;

    public PartnerAvailabilityService(
        IPartnerRepository partnerRepository,
        IPartnerAvailabilityWindowRepository windowRepository,
        IPartnerBlackoutDateRepository blackoutDateRepository)
    {
        _partnerRepository = partnerRepository;
        _windowRepository = windowRepository;
        _blackoutDateRepository = blackoutDateRepository;
    }

    public async Task<PartnerAvailabilityResponse> GetAsync(Guid partnerId)
    {
        var windows = await _windowRepository.GetByPartnerAsync(partnerId);
        var blackoutDates = await _blackoutDateRepository.GetByPartnerAsync(partnerId);

        return new PartnerAvailabilityResponse(
            windows.Select(ToResponse).ToList(),
            blackoutDates.Select(ToResponse).ToList());
    }

    public async Task<Result<IReadOnlyList<PartnerAvailabilityWindowResponse>>> UpdateWindowsAsync(
        Guid partnerId, UpdatePartnerAvailabilityWindowsRequest request)
    {
        if (!await _partnerRepository.ExistsAsync(partnerId))
        {
            return Result.Failure<IReadOnlyList<PartnerAvailabilityWindowResponse>>(
                Error.NotFound("PartnerAvailability.NotFound", "The specified partner does not exist."));
        }

        var windows = request.Windows
            .Select(w => new PartnerAvailabilityWindow(Guid.NewGuid(), partnerId, w.DayOfWeek, w.StartTime, w.EndTime))
            .ToList();
        await _windowRepository.ReplaceForPartnerAsync(partnerId, windows);

        return Result.Success<IReadOnlyList<PartnerAvailabilityWindowResponse>>(windows.Select(ToResponse).ToList());
    }

    public async Task<IReadOnlyList<PartnerBlackoutDateResponse>> GetBlackoutDatesAsync(Guid partnerId)
    {
        var blackoutDates = await _blackoutDateRepository.GetByPartnerAsync(partnerId);
        return blackoutDates.Select(ToResponse).ToList();
    }

    public async Task<Result<PartnerBlackoutDateResponse>> AddBlackoutDateAsync(
        Guid partnerId, AddPartnerBlackoutDateRequest request)
    {
        if (!await _partnerRepository.ExistsAsync(partnerId))
        {
            return Result.Failure<PartnerBlackoutDateResponse>(
                Error.NotFound("PartnerAvailability.NotFound", "The specified partner does not exist."));
        }

        var blackoutDate = new PartnerBlackoutDate(Guid.NewGuid(), partnerId, request.StartDate, request.EndDate, request.Reason);
        await _blackoutDateRepository.AddAsync(blackoutDate);

        return Result.Success(ToResponse(blackoutDate));
    }

    public async Task<Result> DeleteBlackoutDateAsync(Guid partnerId, Guid blackoutDateId)
    {
        var blackoutDate = await _blackoutDateRepository.GetByIdAsync(blackoutDateId);
        if (blackoutDate is null || blackoutDate.PartnerId != partnerId)
        {
            // Same "not found" response whether the row doesn't exist at all or
            // belongs to a different partner - never confirms another
            // partner's blackout date exists (SRS 28.3 IDOR).
            return Result.Failure(Error.NotFound(
                "PartnerAvailability.BlackoutDateNotFound", "The specified blackout date does not exist."));
        }

        await _blackoutDateRepository.DeleteAsync(blackoutDate);
        return Result.Success();
    }

    private static PartnerAvailabilityWindowResponse ToResponse(PartnerAvailabilityWindow window) => new(
        window.Id, window.PartnerId, window.DayOfWeek, window.StartTime, window.EndTime, window.IsActive);

    private static PartnerBlackoutDateResponse ToResponse(PartnerBlackoutDate blackoutDate) => new(
        blackoutDate.Id, blackoutDate.PartnerId, blackoutDate.StartDate, blackoutDate.EndDate, blackoutDate.Reason);
}

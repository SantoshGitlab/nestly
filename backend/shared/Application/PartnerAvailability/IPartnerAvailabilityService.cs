using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.PartnerAvailability;

/// <summary>
/// CRUD over a partner's own recurring availability windows and blackout
/// dates (task 149b, PARTNER.md API surface "Availability"). Thin service
/// over <c>PartnerAvailabilityWindow</c>/<c>PartnerBlackoutDate</c>, the same
/// shape as <c>ISlotManagementService</c>'s window/blackout sections but
/// scoped to one partner instead of admin-wide city configuration.
/// </summary>
public interface IPartnerAvailabilityService
{
    Task<PartnerAvailabilityResponse> GetAsync(Guid partnerId);

    Task<Result<IReadOnlyList<PartnerAvailabilityWindowResponse>>> UpdateWindowsAsync(Guid partnerId, UpdatePartnerAvailabilityWindowsRequest request);

    Task<IReadOnlyList<PartnerBlackoutDateResponse>> GetBlackoutDatesAsync(Guid partnerId);

    Task<Result<PartnerBlackoutDateResponse>> AddBlackoutDateAsync(Guid partnerId, AddPartnerBlackoutDateRequest request);

    Task<Result> DeleteBlackoutDateAsync(Guid partnerId, Guid blackoutDateId);
}

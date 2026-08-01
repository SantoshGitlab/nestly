using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.PartnerProfile;

/// <summary>
/// Partner profile, service-area and skill management (task 149a,
/// PARTNER.md API surface "Profile/Onboarding"). Every method takes the
/// caller's own partner id, resolved from the JWT by the controller — never
/// from a route or body parameter (SRS 28.3 IDOR), mirroring
/// <c>ICustomerProfileService</c>.
/// </summary>
public interface IPartnerProfileService
{
    Task<Result<PartnerProfileResponse>> GetAsync(Guid partnerId);

    Task<Result<PartnerProfileResponse>> UpdateAsync(Guid partnerId, UpdatePartnerProfileRequest request);

    Task<IReadOnlyList<PartnerServiceAreaResponse>> GetServiceAreasAsync(Guid partnerId);

    Task<Result<IReadOnlyList<PartnerServiceAreaResponse>>> UpdateServiceAreasAsync(Guid partnerId, UpdatePartnerServiceAreasRequest request);

    Task<IReadOnlyList<PartnerSkillResponse>> GetSkillsAsync(Guid partnerId);

    Task<Result<IReadOnlyList<PartnerSkillResponse>>> UpdateSkillsAsync(Guid partnerId, UpdatePartnerSkillsRequest request);
}

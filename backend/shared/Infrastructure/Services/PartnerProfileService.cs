using Nestly.Application;
using Nestly.Application.PartnerProfile;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// Partner's own profile, service-area and skill management (task 149a,
/// PARTNER.md API surface "Profile/Onboarding"). KYC document submission and
/// status are handled separately by <see cref="IPartnerKycService"/> — kept
/// out of this service so KYC review workflow (task 150b) stays independent
/// of general profile editing.
/// </summary>
public class PartnerProfileService : IPartnerProfileService
{
    private readonly IPartnerRepository _partnerRepository;
    private readonly IPartnerServiceAreaRepository _serviceAreaRepository;
    private readonly IPartnerSkillMappingRepository _skillMappingRepository;

    public PartnerProfileService(
        IPartnerRepository partnerRepository,
        IPartnerServiceAreaRepository serviceAreaRepository,
        IPartnerSkillMappingRepository skillMappingRepository)
    {
        _partnerRepository = partnerRepository;
        _serviceAreaRepository = serviceAreaRepository;
        _skillMappingRepository = skillMappingRepository;
    }

    public async Task<Result<PartnerProfileResponse>> GetAsync(Guid partnerId)
    {
        var partner = await _partnerRepository.GetByIdAsync(partnerId);
        if (partner is null)
        {
            return Result.Failure<PartnerProfileResponse>(
                Error.NotFound("PartnerProfile.NotFound", "The specified partner does not exist."));
        }

        return Result.Success(ToResponse(partner));
    }

    public async Task<Result<PartnerProfileResponse>> UpdateAsync(Guid partnerId, UpdatePartnerProfileRequest request)
    {
        var partner = await _partnerRepository.GetByIdAsync(partnerId);
        if (partner is null)
        {
            return Result.Failure<PartnerProfileResponse>(
                Error.NotFound("PartnerProfile.NotFound", "The specified partner does not exist."));
        }

        partner.UpdateProfile(request.LegalName, request.DisplayName, request.Email);
        await _partnerRepository.UpdateAsync(partner);

        return Result.Success(ToResponse(partner));
    }

    public async Task<IReadOnlyList<PartnerServiceAreaResponse>> GetServiceAreasAsync(Guid partnerId)
    {
        var areas = await _serviceAreaRepository.GetByPartnerAsync(partnerId);
        return areas.Select(ToResponse).ToList();
    }

    public async Task<Result<IReadOnlyList<PartnerServiceAreaResponse>>> UpdateServiceAreasAsync(
        Guid partnerId, UpdatePartnerServiceAreasRequest request)
    {
        if (!await _partnerRepository.ExistsAsync(partnerId))
        {
            return Result.Failure<IReadOnlyList<PartnerServiceAreaResponse>>(
                Error.NotFound("PartnerProfile.NotFound", "The specified partner does not exist."));
        }

        var areas = request.Areas
            .Select(a => new PartnerServiceArea(Guid.NewGuid(), partnerId, a.CityId, a.ZoneId, a.PincodeId))
            .ToList();
        await _serviceAreaRepository.ReplaceForPartnerAsync(partnerId, areas);

        return Result.Success<IReadOnlyList<PartnerServiceAreaResponse>>(areas.Select(ToResponse).ToList());
    }

    public async Task<IReadOnlyList<PartnerSkillResponse>> GetSkillsAsync(Guid partnerId)
    {
        var skills = await _skillMappingRepository.GetByPartnerAsync(partnerId);
        return skills.Select(ToResponse).ToList();
    }

    public async Task<Result<IReadOnlyList<PartnerSkillResponse>>> UpdateSkillsAsync(
        Guid partnerId, UpdatePartnerSkillsRequest request)
    {
        if (!await _partnerRepository.ExistsAsync(partnerId))
        {
            return Result.Failure<IReadOnlyList<PartnerSkillResponse>>(
                Error.NotFound("PartnerProfile.NotFound", "The specified partner does not exist."));
        }

        var skills = request.Skills
            .Select(s => new PartnerSkillMapping(Guid.NewGuid(), partnerId, s.CategoryId, s.ServiceId))
            .ToList();
        await _skillMappingRepository.ReplaceForPartnerAsync(partnerId, skills);

        return Result.Success<IReadOnlyList<PartnerSkillResponse>>(skills.Select(ToResponse).ToList());
    }

    private static PartnerProfileResponse ToResponse(Partner partner) => new(
        partner.Id, partner.LegalName, partner.DisplayName, partner.Phone, partner.Email,
        partner.Status.ToString(), partner.OnboardingStatus.ToString());

    private static PartnerServiceAreaResponse ToResponse(PartnerServiceArea area) => new(
        area.Id, area.PartnerId, area.CityId, area.ZoneId, area.PincodeId, area.IsActive);

    private static PartnerSkillResponse ToResponse(PartnerSkillMapping skill) => new(
        skill.Id, skill.PartnerId, skill.CategoryId, skill.ServiceId, skill.IsActive);
}

namespace Nestly.Application.PartnerProfile;

/// <summary>Partner's own profile view (PARTNER.md API surface "get/update profile").</summary>
public record PartnerProfileResponse(
    Guid Id,
    string LegalName,
    string DisplayName,
    string Phone,
    string? Email,
    string Status,
    string OnboardingStatus);

public record UpdatePartnerProfileRequest(string LegalName, string DisplayName, string? Email);

/// <summary>One geography a partner covers (PARTNER.md "partner_service_area").</summary>
public record PartnerServiceAreaResponse(
    Guid Id,
    Guid PartnerId,
    Guid CityId,
    Guid? ZoneId,
    Guid? PincodeId,
    bool IsActive);

public record PartnerServiceAreaInput(Guid CityId, Guid? ZoneId, Guid? PincodeId);

/// <summary>Full replacement of a partner's coverage set (PARTNER.md API surface "update service areas").</summary>
public record UpdatePartnerServiceAreasRequest(IReadOnlyList<PartnerServiceAreaInput> Areas);

/// <summary>A category/service a partner is qualified for (PARTNER.md "partner_skill_mapping").</summary>
public record PartnerSkillResponse(
    Guid Id,
    Guid PartnerId,
    Guid CategoryId,
    Guid? ServiceId,
    bool IsActive);

public record PartnerSkillInput(Guid CategoryId, Guid? ServiceId);

/// <summary>Full replacement of a partner's declared skills (PARTNER.md API surface "update skills").</summary>
public record UpdatePartnerSkillsRequest(IReadOnlyList<PartnerSkillInput> Skills);

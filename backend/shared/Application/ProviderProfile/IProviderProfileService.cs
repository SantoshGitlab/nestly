using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.ProviderProfile;

/// <summary>
/// Provider profile, service-area and skill management (task 149a,
/// PROVIDER.md API surface "Profile/Onboarding"). Every method takes the
/// caller's own provider id, resolved from the JWT by the controller — never
/// from a route or body parameter (SRS 28.3 IDOR), mirroring
/// <c>ICustomerProfileService</c>.
/// </summary>
public interface IProviderProfileService
{
    Task<Result<ProviderProfileResponse>> GetAsync(Guid providerId);

    Task<Result<ProviderProfileResponse>> UpdateAsync(Guid providerId, UpdateProviderProfileRequest request);

    /// <summary>
    /// Sets or clears the provider's profile photo (task 293). Setting one
    /// always puts it back into moderation - customers only ever see an
    /// approved photo (<see cref="Nestly.Domain.Provider.PublicPhotoUrl"/>).
    /// </summary>
    Task<Result<ProviderProfileResponse>> UpdatePhotoAsync(Guid providerId, UpdateProviderPhotoRequest request);

    Task<IReadOnlyList<ProviderServiceAreaResponse>> GetServiceAreasAsync(Guid providerId);

    Task<Result<IReadOnlyList<ProviderServiceAreaResponse>>> UpdateServiceAreasAsync(Guid providerId, UpdateProviderServiceAreasRequest request);

    Task<IReadOnlyList<ProviderSkillResponse>> GetSkillsAsync(Guid providerId);

    Task<Result<IReadOnlyList<ProviderSkillResponse>>> UpdateSkillsAsync(Guid providerId, UpdateProviderSkillsRequest request);

    /// <summary>
    /// Self-service right-to-erasure account deletion (mirrors
    /// <c>ICustomerProfileService.DeleteAccountAsync</c>). Terminal and
    /// irreversible.
    /// </summary>
    Task<Result> DeleteAccountAsync(Guid providerId);
}

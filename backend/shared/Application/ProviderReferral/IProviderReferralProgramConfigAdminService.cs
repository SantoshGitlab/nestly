using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.ProviderReferral;

/// <summary>Admin CRUD for the single ProviderReferralProgramConfig row, mirrors IReferralProgramConfigAdminService (milestones intentionally not included in this v1).</summary>
public interface IProviderReferralProgramConfigAdminService
{
    Task<Result<ProviderReferralProgramConfigResponse>> GetAsync();

    Task<Result<ProviderReferralProgramConfigResponse>> UpdateAsync(ProviderReferralProgramConfigUpdateRequest request, Guid adminUserId);
}

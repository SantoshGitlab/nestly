using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.ProviderReferral;

/// <summary>Backs the admin-api's provider-referral list/detail surface, mirrors IReferralAdminService (funnel/cost reports intentionally not included in this v1 - see PROVIDER-REFERRAL.md).</summary>
public interface IProviderReferralAdminService
{
    Task<ProviderReferralAdminSearchResponse> SearchAsync(ProviderReferralAdminSearchRequest request);

    Task<Result<ProviderReferralAdminDetailResponse>> GetByIdAsync(Guid id);
}

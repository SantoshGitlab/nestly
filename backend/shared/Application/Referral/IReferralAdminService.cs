using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.Referral;

/// <summary>Backs the admin-api's referral list/detail and reporting surface (tasks 170, 171).</summary>
public interface IReferralAdminService
{
    Task<ReferralAdminSearchResponse> SearchAsync(ReferralAdminSearchRequest request);

    Task<Result<ReferralAdminDetailResponse>> GetByIdAsync(Guid id);

    Task<Result<ReferralFunnelReportResponse>> GetFunnelReportAsync(DateTime? fromUtc, DateTime? toUtc);

    Task<Result<ReferralCostReportResponse>> GetCostReportAsync(DateTime? fromUtc, DateTime? toUtc);
}

using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.Referral;

/// <summary>
/// Admin CRUD for the single <see cref="Domain.ReferralProgramConfig"/> row
/// plus task 174's milestone tiers (task 167). Mirrors the shape of other
/// single-row admin config surfaces in this codebase (e.g. SystemSettings'
/// per-group GET/PUT).
/// </summary>
public interface IReferralProgramConfigAdminService
{
    Task<Result<ReferralProgramConfigResponse>> GetAsync();

    Task<Result<ReferralProgramConfigResponse>> UpdateAsync(ReferralProgramConfigUpdateRequest request, Guid adminUserId);

    /// <summary>All milestone tiers, active and inactive, ascending by threshold.</summary>
    Task<IReadOnlyList<ReferralMilestoneResponse>> ListMilestonesAsync();

    Task<Result<ReferralMilestoneResponse>> CreateMilestoneAsync(ReferralMilestoneCreateRequest request);

    Task<Result<ReferralMilestoneResponse>> SetMilestoneActiveAsync(Guid milestoneId, bool isActive);
}

using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.ProviderReferral;

/// <summary>Manual fraud review queue for provider referrals, mirrors IReferralFraudReviewService.</summary>
public interface IProviderReferralFraudReviewService
{
    /// <summary>adminUserId is null for a system-detected signal rather than a manual admin flag.</summary>
    Task<Result> FlagAsync(Guid referralId, Guid? adminUserId, string? note);

    /// <summary>Admin confirms the flagged signal was a real abuse pattern - the flag clears; any actual reward reversal is a separate, deliberate action through existing earning-ledger adjustment tooling.</summary>
    Task<Result> ApproveAsync(Guid referralId, Guid adminUserId, string? note);

    /// <summary>Admin determined the flag was a false positive - the flag clears, no further action.</summary>
    Task<Result> RejectAsync(Guid referralId, Guid adminUserId, string? note);
}

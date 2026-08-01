using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.Referral;

/// <summary>
/// Task 166 (REFERRAL.md "FRAUD / ABUSE PREVENTION" - "manual review queue,
/// not auto-block, for soft signals"). See <c>Referral.IsFraudFlagged</c>'s
/// doc comment for why this is an independent flag rather than a Status
/// value, and why approving a flag never auto-reverses a reward.
/// </summary>
public interface IReferralFraudReviewService
{
    /// <summary>adminUserId is null for a system-detected signal (e.g. the qualifying booking being cancelled shortly after reward) rather than a manual admin flag.</summary>
    Task<Result> FlagAsync(Guid referralId, Guid? adminUserId, string? note);

    /// <summary>Admin confirms the flagged signal was a real abuse pattern - the flag clears; any actual reward reversal is a separate, deliberate action through existing wallet/coupon tooling.</summary>
    Task<Result> ApproveAsync(Guid referralId, Guid adminUserId, string? note);

    /// <summary>Admin determined the flag was a false positive - the flag clears, no further action.</summary>
    Task<Result> RejectAsync(Guid referralId, Guid adminUserId, string? note);
}

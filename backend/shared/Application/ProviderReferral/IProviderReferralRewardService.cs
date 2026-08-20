namespace Nestly.Application.ProviderReferral;

/// <summary>
/// Disburses a Qualified provider referral's reward to both sides via the
/// provider earning ledger and marks it Rewarded, mirrors
/// <c>IReferralRewardService</c>. Called synchronously right after
/// qualification, not as a separately scheduled step.
/// </summary>
public interface IProviderReferralRewardService
{
    Task DisburseAsync(Nestly.Domain.ProviderReferral referral);
}

namespace Nestly.Application.Referral;

/// <summary>
/// Disburses a Qualified referral's reward to both sides and marks it
/// Rewarded (REFERRAL.md, task 165). Called synchronously right after
/// qualification (task 164's handler), not as a separately scheduled step -
/// REFERRAL.md's DECISIONS section is explicit that both sides are rewarded
/// on the same event as qualification, which closes the "throwaway account
/// for the signup bonus" loophole.
/// </summary>
public interface IReferralRewardService
{
    Task DisburseAsync(Domain.Referral referral);
}

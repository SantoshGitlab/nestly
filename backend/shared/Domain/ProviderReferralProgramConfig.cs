using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>
/// Admin-editable provider referral program settings (PROVIDER-REFERRAL.md
/// "DATA MODEL"). Mirrors <see cref="ReferralProgramConfig"/>'s shape and its
/// single-mutable-row convention, adapted for the supply side:
///
/// <list type="bullet">
/// <item>Both sides are always rewarded via the provider earning ledger
/// (<see cref="ProviderEarningSourceType.ProviderReferralReward"/>) - there is
/// no coupon-reward option, unlike the customer program, because a coupon is
/// a customer-facing discount instrument that has no equivalent meaning for a
/// provider's own earnings.</item>
/// <item>Qualification is the referee completing
/// <see cref="QualifyingCompletedJobsCount"/> jobs, not a single booking
/// amount threshold - a provider referral pays out real money for a new
/// *worker*, not a single transaction, so vesting over several completed
/// jobs (rather than "any first booking") is the fraud control: a fake or
/// disinterested account that never actually works never qualifies.</item>
/// </list>
///
/// Reward terms are snapshotted onto <see cref="ProviderReferral"/> at
/// registration time, same non-retroactivity reasoning as
/// <see cref="ReferralProgramConfig"/>'s own doc comment.
/// </summary>
public class ProviderReferralProgramConfig : Entity<Guid>
{
    public decimal ReferrerRewardValue { get; private set; }
    public decimal RefereeRewardValue { get; private set; }

    /// <summary>How many of the referee's own bookings must reach Completed before the referral qualifies (PROVIDER-REFERRAL.md "HOW IT WORKS").</summary>
    public int QualifyingCompletedJobsCount { get; private set; }

    /// <summary>Days after registration before an unqualified referral expires (mirrors ReferralProgramConfig.ReferralExpiryDays).</summary>
    public int ReferralExpiryDays { get; private set; }

    /// <summary>Fraud cap: max referrals one referrer can be rewarded for. Null = unlimited.</summary>
    public int? MaxReferralsPerProvider { get; private set; }

    public bool IsActive { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }
    public Guid? UpdatedByAdminUserId { get; private set; }

    protected ProviderReferralProgramConfig() { }

    public ProviderReferralProgramConfig(
        Guid id,
        decimal referrerRewardValue,
        decimal refereeRewardValue,
        int qualifyingCompletedJobsCount,
        int referralExpiryDays,
        int? maxReferralsPerProvider,
        bool isActive)
        : base(id)
    {
        Validate(referrerRewardValue, refereeRewardValue, qualifyingCompletedJobsCount, referralExpiryDays, maxReferralsPerProvider);

        ReferrerRewardValue = referrerRewardValue;
        RefereeRewardValue = refereeRewardValue;
        QualifyingCompletedJobsCount = qualifyingCompletedJobsCount;
        ReferralExpiryDays = referralExpiryDays;
        MaxReferralsPerProvider = maxReferralsPerProvider;
        IsActive = isActive;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Update(
        decimal referrerRewardValue,
        decimal refereeRewardValue,
        int qualifyingCompletedJobsCount,
        int referralExpiryDays,
        int? maxReferralsPerProvider,
        bool isActive,
        Guid updatedByAdminUserId)
    {
        Validate(referrerRewardValue, refereeRewardValue, qualifyingCompletedJobsCount, referralExpiryDays, maxReferralsPerProvider);

        ReferrerRewardValue = referrerRewardValue;
        RefereeRewardValue = refereeRewardValue;
        QualifyingCompletedJobsCount = qualifyingCompletedJobsCount;
        ReferralExpiryDays = referralExpiryDays;
        MaxReferralsPerProvider = maxReferralsPerProvider;
        IsActive = isActive;
        UpdatedAtUtc = DateTime.UtcNow;
        UpdatedByAdminUserId = updatedByAdminUserId;
    }

    private static void Validate(
        decimal referrerRewardValue, decimal refereeRewardValue, int qualifyingCompletedJobsCount,
        int referralExpiryDays, int? maxReferralsPerProvider)
    {
        if (referrerRewardValue <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(referrerRewardValue), "Referrer reward value must be positive.");
        }

        if (refereeRewardValue <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(refereeRewardValue), "Referee reward value must be positive.");
        }

        if (qualifyingCompletedJobsCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(qualifyingCompletedJobsCount), "Qualifying completed jobs count must be positive.");
        }

        if (referralExpiryDays <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(referralExpiryDays), "Referral expiry days must be positive.");
        }

        if (maxReferralsPerProvider is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxReferralsPerProvider), "Max referrals per provider must be positive when set.");
        }
    }
}

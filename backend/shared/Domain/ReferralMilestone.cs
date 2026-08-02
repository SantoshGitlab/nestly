using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>
/// An admin-defined bonus tier on top of the per-referral reward (REFERRAL.md
/// "FUTURE ENHANCEMENTS", task 174): once a referrer's qualified-referral
/// count crosses <see cref="ThresholdCount"/>, they receive one extra
/// <see cref="BonusType"/>/<see cref="BonusValue"/> reward, disbursed through
/// the same wallet-credit/coupon mechanics <see cref="ReferralRewardType"/>
/// already governs for a normal per-referral reward - not a second reward
/// system. Seeded/managed the same simple way <see cref="ReferralProgramConfig"/>
/// is (no admin UI wired up for this yet - no task in this phase asks for
/// one; the mechanism is what's requested).
/// </summary>
public class ReferralMilestone : Entity<Guid>
{
    /// <summary>The referrer's rewarded-referral count that triggers this milestone (e.g. 5th, 10th referral).</summary>
    public int ThresholdCount { get; private set; }

    public ReferralRewardType BonusType { get; private set; }
    public decimal BonusValue { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    protected ReferralMilestone() { }

    public ReferralMilestone(Guid id, int thresholdCount, ReferralRewardType bonusType, decimal bonusValue, bool isActive)
        : base(id)
    {
        if (thresholdCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(thresholdCount), "Milestone threshold must be positive.");
        }

        if (bonusValue <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bonusValue), "Milestone bonus value must be positive.");
        }

        ThresholdCount = thresholdCount;
        BonusType = bonusType;
        BonusValue = bonusValue;
        IsActive = isActive;
        CreatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Admin toggle (task 167's config surface) - threshold/bonus type/value are immutable once created, same "identity fields fixed, status toggleable" pattern as <c>Coupon</c>'s activate/deactivate.</summary>
    public void SetActive(bool isActive) => IsActive = isActive;
}

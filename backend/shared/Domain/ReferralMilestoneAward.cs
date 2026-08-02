using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>
/// Records that a referrer already received a given <see cref="ReferralMilestone"/>'s
/// bonus - task 174's idempotency guard, so a referrer's count re-crossing or
/// exceeding a threshold on a later referral never pays the same milestone
/// twice. One row per (milestone, referrer) pair, enforced by a unique index
/// (see <c>ReferralMilestoneAwardConfiguration</c>) - mirrors how
/// <see cref="Referral"/> tracks its own <c>WalletEntryId</c>/<c>CouponId</c>
/// for audit purposes.
/// </summary>
public class ReferralMilestoneAward : Entity<Guid>
{
    public Guid ReferralMilestoneId { get; private set; }
    public Guid ReferrerCustomerId { get; private set; }
    public Guid? WalletEntryId { get; private set; }
    public Guid? CouponId { get; private set; }
    public DateTime AwardedAtUtc { get; private set; }

    protected ReferralMilestoneAward() { }

    public ReferralMilestoneAward(
        Guid id, Guid referralMilestoneId, Guid referrerCustomerId, Guid? walletEntryId, Guid? couponId)
        : base(id)
    {
        ReferralMilestoneId = referralMilestoneId;
        ReferrerCustomerId = referrerCustomerId;
        WalletEntryId = walletEntryId;
        CouponId = couponId;
        AwardedAtUtc = DateTime.UtcNow;
    }
}

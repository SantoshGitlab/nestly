using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>
/// One referral relationship: a referrer's code used by a referee at
/// registration (REFERRAL.md "HOW IT WORKS", task 161). Reward terms
/// (<see cref="ReferrerRewardType"/> through <see cref="MinQualifyingOrderAmount"/>)
/// are snapshotted from <see cref="ReferralProgramConfig"/> at construction
/// time - see that class's doc comment for why - so this row is immune to
/// later admin config changes.
/// </summary>
public class Referral : Entity<Guid>
{
    public Guid ReferrerCustomerId { get; private set; }
    public Guid RefereeCustomerId { get; private set; }
    public string ReferralCodeUsed { get; private set; } = string.Empty;
    public ReferralStatus Status { get; private set; }

    public Guid? QualifyingBookingId { get; private set; }

    public ReferralRewardType ReferrerRewardType { get; private set; }
    public decimal ReferrerRewardValue { get; private set; }
    public ReferralRewardType RefereeRewardType { get; private set; }
    public decimal RefereeRewardValue { get; private set; }
    public decimal MinQualifyingOrderAmount { get; private set; }

    /// <summary>Wallet-credit rewards: the WalletLedgerEntry id credited to the referrer. Null until Rewarded, or always null for coupon rewards.</summary>
    public Guid? ReferrerWalletEntryId { get; private set; }

    /// <summary>Coupon rewards: the single-use Coupon id issued to the referrer. Null until Rewarded, or always null for wallet-credit rewards.</summary>
    public Guid? ReferrerCouponId { get; private set; }

    public Guid? RefereeWalletEntryId { get; private set; }
    public Guid? RefereeCouponId { get; private set; }

    public DateTime RegisteredAtUtc { get; private set; }
    public DateTime? QualifiedAtUtc { get; private set; }
    public DateTime? RewardedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }

    /// <summary>
    /// Task 166 fraud review (REFERRAL.md "FRAUD / ABUSE PREVENTION"'s
    /// "manual review queue, not auto-block, for soft signals"). Independent
    /// of <see cref="Status"/>, same shape as <see cref="Review.IsFlagged"/> -
    /// a Rewarded referral can still be flagged for after-the-fact review
    /// (e.g. "qualifying booking cancelled right after reward"), which a
    /// Status-based FraudFlagged state couldn't represent cleanly since
    /// reward disbursement runs synchronously with qualification (task
    /// 164-165's DisburseAsync), leaving no real pre-reward review window
    /// for automated flags to act on before money moves. Approving a flag
    /// only records the admin's finding here - it never auto-reverses a
    /// wallet credit or deactivates a coupon, since the wallet ledger is
    /// append-only (SRS 14.5) and safely reversing only the unspent portion
    /// of ONE specific credit needs the FIFO consumption-tracking design
    /// task 175 explicitly defers; any actual clawback is a deliberate,
    /// separate admin action through existing wallet/coupon tooling.
    /// </summary>
    public bool IsFraudFlagged { get; private set; }

    public string? FraudReviewNote { get; private set; }
    public Guid? FraudReviewedByAdminUserId { get; private set; }
    public DateTime? FraudReviewedAtUtc { get; private set; }

    protected Referral() { }

    public Referral(
        Guid id,
        Guid referrerCustomerId,
        Guid refereeCustomerId,
        string referralCodeUsed,
        ReferralProgramConfig config)
        : base(id)
    {
        if (referrerCustomerId == refereeCustomerId)
        {
            throw new InvalidOperationException("A customer cannot refer themselves.");
        }

        if (string.IsNullOrWhiteSpace(referralCodeUsed))
        {
            throw new ArgumentException("Referral code is required.", nameof(referralCodeUsed));
        }

        ReferrerCustomerId = referrerCustomerId;
        RefereeCustomerId = refereeCustomerId;
        ReferralCodeUsed = referralCodeUsed;
        Status = ReferralStatus.Registered;

        ReferrerRewardType = config.ReferrerRewardType;
        ReferrerRewardValue = config.ReferrerRewardValue;
        RefereeRewardType = config.RefereeRewardType;
        RefereeRewardValue = config.RefereeRewardValue;
        MinQualifyingOrderAmount = config.MinQualifyingOrderAmount;

        RegisteredAtUtc = DateTime.UtcNow;
        ExpiresAtUtc = RegisteredAtUtc.AddDays(config.ReferralExpiryDays);
    }

    /// <summary>Task 164: the referee's first booking met the qualifying amount.</summary>
    public void MarkQualified(Guid qualifyingBookingId)
    {
        if (Status != ReferralStatus.Registered)
        {
            throw new InvalidOperationException($"Cannot qualify a referral in status {Status}.");
        }

        Status = ReferralStatus.Qualified;
        QualifyingBookingId = qualifyingBookingId;
        QualifiedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Task 165: reward disbursed to both sides.</summary>
    public void MarkRewarded(
        Guid? referrerWalletEntryId, Guid? referrerCouponId,
        Guid? refereeWalletEntryId, Guid? refereeCouponId)
    {
        if (Status != ReferralStatus.Qualified)
        {
            throw new InvalidOperationException($"Cannot reward a referral in status {Status}.");
        }

        Status = ReferralStatus.Rewarded;
        ReferrerWalletEntryId = referrerWalletEntryId;
        ReferrerCouponId = referrerCouponId;
        RefereeWalletEntryId = refereeWalletEntryId;
        RefereeCouponId = refereeCouponId;
        RewardedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Task 166: an admin raised (or system detected) a soft abuse signal - see <see cref="IsFraudFlagged"/>'s doc comment for why this doesn't touch <see cref="Status"/>.</summary>
    public void Flag(Guid? adminUserId, string? note)
    {
        IsFraudFlagged = true;
        FraudReviewNote = note;
        FraudReviewedByAdminUserId = adminUserId;
        FraudReviewedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Task 166: admin resolved the flag - confirmed fraud (any actual reversal happens separately, see <see cref="IsFraudFlagged"/>) or false positive. Either way the flag itself clears.</summary>
    public void Unflag(Guid adminUserId, string? note)
    {
        IsFraudFlagged = false;
        FraudReviewNote = note;
        FraudReviewedByAdminUserId = adminUserId;
        FraudReviewedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Scheduled sweep: no qualifying booking within the expiry window.</summary>
    public void MarkExpired()
    {
        if (Status != ReferralStatus.Registered)
        {
            throw new InvalidOperationException($"Cannot expire a referral in status {Status}.");
        }

        Status = ReferralStatus.Expired;
    }
}

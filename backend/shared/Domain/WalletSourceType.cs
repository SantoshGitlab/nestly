namespace Nestly.Domain;

/// <summary>
/// The kind of event that produced a <see cref="WalletLedgerEntry"/> (SRS
/// 14.5 - "every credit/debit must reference source event"). Paired with
/// <see cref="WalletLedgerEntry.SourceReferenceId"/> for the concrete row.
/// </summary>
public enum WalletSourceType
{
    /// <summary>Credited from a <see cref="RefundTransaction"/> settled to wallet.</summary>
    Refund,

    /// <summary>Promotional/goodwill credit with no source aggregate (SourceReferenceId is null).</summary>
    PromotionalCredit,

    /// <summary>Manual adjustment (support/admin correction), no source aggregate.</summary>
    ManualAdjustment,

    /// <summary>Credited from a qualifying <see cref="Referral"/>'s reward disbursement (REFERRAL.md, task 165). SourceReferenceId is the Referral id.</summary>
    ReferralReward,

    /// <summary>Credited from a referrer crossing a <see cref="ReferralMilestone"/> threshold (task 174). SourceReferenceId is the ReferralMilestone id.</summary>
    ReferralMilestoneBonus,

    /// <summary>Debited by the expiry sweep (task 175) for the unconsumed portion of an expiring wallet credit. SourceReferenceId is the expiring WalletLedgerEntry's id.</summary>
    ReferralCreditExpiry,

    /// <summary>Credited from a qualifying order's Nestly Coins reward (docs/NESTLY-COINS.md, task 201). SourceReferenceId is the completed Booking's id.</summary>
    NestlyCoinsReward
}

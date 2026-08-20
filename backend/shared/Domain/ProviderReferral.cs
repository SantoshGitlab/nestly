using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>
/// One provider-referral relationship: a referrer's code used by a referee
/// provider at registration (PROVIDER-REFERRAL.md "HOW IT WORKS"). Mirrors
/// <see cref="Referral"/> structurally; see
/// <see cref="ProviderReferralProgramConfig"/>'s doc comment for the two
/// substantive differences (no coupon reward option, and qualification is a
/// completed-job count rather than a single booking amount).
/// </summary>
public class ProviderReferral : Entity<Guid>
{
    public Guid ReferrerProviderId { get; private set; }
    public Guid RefereeProviderId { get; private set; }
    public string ReferralCodeUsed { get; private set; } = string.Empty;
    public ProviderReferralStatus Status { get; private set; }

    public Guid? QualifyingBookingId { get; private set; }

    public decimal ReferrerRewardValue { get; private set; }
    public decimal RefereeRewardValue { get; private set; }
    public int QualifyingCompletedJobsCount { get; private set; }

    /// <summary>The ProviderEarningLedgerEntry id credited to the referrer. Null until Rewarded, or always null if the per-referrer cap skipped this side.</summary>
    public Guid? ReferrerEarningEntryId { get; private set; }

    /// <summary>The ProviderEarningLedgerEntry id credited to the referee. Null until Rewarded.</summary>
    public Guid? RefereeEarningEntryId { get; private set; }

    public DateTime RegisteredAtUtc { get; private set; }
    public DateTime? QualifiedAtUtc { get; private set; }
    public DateTime? RewardedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }

    /// <summary>Fraud review flag, independent of <see cref="Status"/> - same reasoning as <see cref="Referral.IsFraudFlagged"/>'s doc comment: a Rewarded referral can still be flagged after the fact, and approving a flag never auto-reverses an earning-ledger credit (the ledger is append-only; any clawback is a deliberate separate admin adjustment).</summary>
    public bool IsFraudFlagged { get; private set; }

    public string? FraudReviewNote { get; private set; }
    public Guid? FraudReviewedByAdminUserId { get; private set; }
    public DateTime? FraudReviewedAtUtc { get; private set; }

    protected ProviderReferral() { }

    public ProviderReferral(
        Guid id,
        Guid referrerProviderId,
        Guid refereeProviderId,
        string referralCodeUsed,
        ProviderReferralProgramConfig config)
        : base(id)
    {
        if (referrerProviderId == refereeProviderId)
        {
            throw new InvalidOperationException("A provider cannot refer themselves.");
        }

        if (string.IsNullOrWhiteSpace(referralCodeUsed))
        {
            throw new ArgumentException("Referral code is required.", nameof(referralCodeUsed));
        }

        ReferrerProviderId = referrerProviderId;
        RefereeProviderId = refereeProviderId;
        ReferralCodeUsed = referralCodeUsed;
        Status = ProviderReferralStatus.Registered;

        ReferrerRewardValue = config.ReferrerRewardValue;
        RefereeRewardValue = config.RefereeRewardValue;
        QualifyingCompletedJobsCount = config.QualifyingCompletedJobsCount;

        RegisteredAtUtc = DateTime.UtcNow;
        ExpiresAtUtc = RegisteredAtUtc.AddDays(config.ReferralExpiryDays);
    }

    /// <summary>The referee reached the configured completed-job count.</summary>
    public void MarkQualified(Guid qualifyingBookingId)
    {
        if (Status != ProviderReferralStatus.Registered)
        {
            throw new InvalidOperationException($"Cannot qualify a provider referral in status {Status}.");
        }

        Status = ProviderReferralStatus.Qualified;
        QualifyingBookingId = qualifyingBookingId;
        QualifiedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Reward disbursed to both sides (or just the referee, if the referrer's cap was reached).</summary>
    public void MarkRewarded(Guid? referrerEarningEntryId, Guid? refereeEarningEntryId)
    {
        if (Status != ProviderReferralStatus.Qualified)
        {
            throw new InvalidOperationException($"Cannot reward a provider referral in status {Status}.");
        }

        Status = ProviderReferralStatus.Rewarded;
        ReferrerEarningEntryId = referrerEarningEntryId;
        RefereeEarningEntryId = refereeEarningEntryId;
        RewardedAtUtc = DateTime.UtcNow;
    }

    /// <summary>An admin raised (or the system detected) a soft abuse signal - see <see cref="IsFraudFlagged"/>'s doc comment for why this doesn't touch <see cref="Status"/>.</summary>
    public void Flag(Guid? adminUserId, string? note)
    {
        IsFraudFlagged = true;
        FraudReviewNote = note;
        FraudReviewedByAdminUserId = adminUserId;
        FraudReviewedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Admin resolved the flag - confirmed fraud or false positive. Either way the flag itself clears; any reversal is a separate deliberate action.</summary>
    public void Unflag(Guid adminUserId, string? note)
    {
        IsFraudFlagged = false;
        FraudReviewNote = note;
        FraudReviewedByAdminUserId = adminUserId;
        FraudReviewedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Scheduled sweep: the referee never reached the qualifying job count within the expiry window.</summary>
    public void MarkExpired()
    {
        if (Status != ProviderReferralStatus.Registered)
        {
            throw new InvalidOperationException($"Cannot expire a provider referral in status {Status}.");
        }

        Status = ProviderReferralStatus.Expired;
    }
}

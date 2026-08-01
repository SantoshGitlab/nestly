using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>
/// Admin-editable referral program settings (REFERRAL.md "DATA MODEL",
/// task 161). A single mutable row, following the same shape
/// <c>SystemSetting</c> uses for one-admin-editable-row config
/// (<c>SystemSettingsController</c>'s per-group GET/PUT pattern) rather
/// than a generic JSON blob, since these fields are typed and queried
/// directly (task 164's qualifying-amount check).
///
/// Deliberately has no <c>effective_from</c>/<c>effective_to</c> versioning
/// despite REFERRAL.md's data model section naming those columns: reward
/// terms are snapshotted onto <see cref="Referral"/> at registration time
/// instead (mirroring this codebase's established booking-price-snapshot
/// convention, see <c>Booking</c>) so a later admin change to this config
/// can never retroactively alter a reward already promised to an in-flight
/// referral. Versioned config rows would achieve the same non-retroactivity
/// with strictly more moving parts for no behavioural gain - snapshotting
/// the one row that exists is simpler and reuses a pattern this codebase
/// already trusts.
/// </summary>
public class ReferralProgramConfig : Entity<Guid>
{
    public ReferralRewardType ReferrerRewardType { get; private set; }
    public decimal ReferrerRewardValue { get; private set; }
    public ReferralRewardType RefereeRewardType { get; private set; }
    public decimal RefereeRewardValue { get; private set; }

    /// <summary>Minimum qualifying-booking order amount for the referee's booking to count (REFERRAL.md "HOW IT WORKS").</summary>
    public decimal MinQualifyingOrderAmount { get; private set; }

    /// <summary>Days after registration before an unqualified referral expires (REFERRAL.md "HOW IT WORKS").</summary>
    public int ReferralExpiryDays { get; private set; }

    /// <summary>Fraud cap: max referrals one referrer can be rewarded for. Null = unlimited (REFERRAL.md "FRAUD / ABUSE PREVENTION").</summary>
    public int? MaxReferralsPerCustomer { get; private set; }

    public bool IsActive { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }
    public Guid? UpdatedByAdminUserId { get; private set; }

    protected ReferralProgramConfig() { }

    public ReferralProgramConfig(
        Guid id,
        ReferralRewardType referrerRewardType,
        decimal referrerRewardValue,
        ReferralRewardType refereeRewardType,
        decimal refereeRewardValue,
        decimal minQualifyingOrderAmount,
        int referralExpiryDays,
        int? maxReferralsPerCustomer,
        bool isActive)
        : base(id)
    {
        Validate(referrerRewardValue, refereeRewardValue, minQualifyingOrderAmount, referralExpiryDays, maxReferralsPerCustomer);

        ReferrerRewardType = referrerRewardType;
        ReferrerRewardValue = referrerRewardValue;
        RefereeRewardType = refereeRewardType;
        RefereeRewardValue = refereeRewardValue;
        MinQualifyingOrderAmount = minQualifyingOrderAmount;
        ReferralExpiryDays = referralExpiryDays;
        MaxReferralsPerCustomer = maxReferralsPerCustomer;
        IsActive = isActive;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Update(
        ReferralRewardType referrerRewardType,
        decimal referrerRewardValue,
        ReferralRewardType refereeRewardType,
        decimal refereeRewardValue,
        decimal minQualifyingOrderAmount,
        int referralExpiryDays,
        int? maxReferralsPerCustomer,
        bool isActive,
        Guid updatedByAdminUserId)
    {
        Validate(referrerRewardValue, refereeRewardValue, minQualifyingOrderAmount, referralExpiryDays, maxReferralsPerCustomer);

        ReferrerRewardType = referrerRewardType;
        ReferrerRewardValue = referrerRewardValue;
        RefereeRewardType = refereeRewardType;
        RefereeRewardValue = refereeRewardValue;
        MinQualifyingOrderAmount = minQualifyingOrderAmount;
        ReferralExpiryDays = referralExpiryDays;
        MaxReferralsPerCustomer = maxReferralsPerCustomer;
        IsActive = isActive;
        UpdatedAtUtc = DateTime.UtcNow;
        UpdatedByAdminUserId = updatedByAdminUserId;
    }

    private static void Validate(
        decimal referrerRewardValue, decimal refereeRewardValue, decimal minQualifyingOrderAmount,
        int referralExpiryDays, int? maxReferralsPerCustomer)
    {
        if (referrerRewardValue <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(referrerRewardValue), "Referrer reward value must be positive.");
        }

        if (refereeRewardValue <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(refereeRewardValue), "Referee reward value must be positive.");
        }

        if (minQualifyingOrderAmount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minQualifyingOrderAmount), "Minimum qualifying order amount cannot be negative.");
        }

        if (referralExpiryDays <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(referralExpiryDays), "Referral expiry days must be positive.");
        }

        if (maxReferralsPerCustomer is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxReferralsPerCustomer), "Max referrals per customer must be positive when set.");
        }
    }
}

namespace Nestly.Domain;

/// <summary>Lifecycle state of a <see cref="Referral"/> (REFERRAL.md "HOW IT WORKS").</summary>
public enum ReferralStatus
{
    /// <summary>The referee registered using the referrer's code; no qualifying booking yet.</summary>
    Registered,

    /// <summary>The referee completed a qualifying booking (amount at/above the configured minimum); reward not yet disbursed.</summary>
    Qualified,

    /// <summary>Reward disbursed to both sides. Terminal.</summary>
    Rewarded,

    /// <summary>No qualifying booking within the configured expiry window. Terminal - no reward, no error.</summary>
    Expired
}

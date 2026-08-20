namespace Nestly.Domain;

/// <summary>Lifecycle state of a <see cref="ProviderReferral"/> (PROVIDER-REFERRAL.md "HOW IT WORKS").</summary>
public enum ProviderReferralStatus
{
    /// <summary>The referee provider registered using the referrer's code; the qualifying completed-job count has not been reached yet.</summary>
    Registered,

    /// <summary>The referee reached the configured number of completed jobs; reward not yet disbursed.</summary>
    Qualified,

    /// <summary>Reward disbursed to both sides. Terminal.</summary>
    Rewarded,

    /// <summary>The referee did not reach the qualifying job count within the configured expiry window. Terminal - no reward, no error.</summary>
    Expired
}

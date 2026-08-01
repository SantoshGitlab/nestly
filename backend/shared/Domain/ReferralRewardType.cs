namespace Nestly.Domain;

/// <summary>How a referral reward is paid out (REFERRAL.md "DATA MODEL", "DESIGN PRINCIPLE: REUSE, DON'T DUPLICATE"). Admin-configured per side on <see cref="ReferralProgramConfig"/>, not fixed platform-wide (REFERRAL.md OPEN DECISIONS #1).</summary>
public enum ReferralRewardType
{
    /// <summary>Credited to WalletLedgerEntry via WalletSourceType.ReferralReward.</summary>
    WalletCredit,

    /// <summary>A single-use Coupon row issued to the specific recipient.</summary>
    Coupon
}

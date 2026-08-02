namespace Nestly.Application.Referral;

/// <summary>Customer-facing summary for the Refer &amp; Earn screen (task 168, REFERRAL.md "GET /me/referral").</summary>
public record ReferralSummaryResponse(
    string ReferralCode,
    string ShareLink,
    int InvitedCount,
    int QualifiedCount,
    int RewardedCount,
    /// <summary>Sum of the referrer-side reward value actually paid out across every Rewarded referral (per-referral rewards only - milestone bonuses are tracked separately and not yet folded into this total).</summary>
    decimal TotalEarned);

/// <summary>One referral in the customer's own history (task 168, REFERRAL.md "GET /me/referral/history").</summary>
public record ReferralHistoryItemResponse(
    Guid Id,
    string RefereeName,
    string Status,
    DateTime RegisteredAtUtc,
    DateTime? QualifiedAtUtc,
    DateTime? RewardedAtUtc,
    /// <summary>The referrer's reward value for this referral - null while not yet Rewarded, or if the per-customer cap skipped this referral's referrer-side reward.</summary>
    decimal? RewardEarned);

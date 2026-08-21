using Nestly.Domain;

namespace Nestly.Application.ProviderReferral;

// ---- Provider self-service (provider-api) ----

/// <summary>Provider-facing summary for the Refer &amp; Earn screen, mirrors ReferralSummaryResponse.</summary>
public record ProviderReferralSummaryResponse(
    string ReferralCode,
    string ShareLink,
    int InvitedCount,
    int QualifiedCount,
    int RewardedCount,
    /// <summary>Sum of the referrer-side reward value actually paid out across every Rewarded referral.</summary>
    decimal TotalEarned);

/// <summary>One referral in the provider's own history as referrer, mirrors ReferralHistoryItemResponse.</summary>
public record ProviderReferralHistoryItemResponse(
    Guid Id,
    string RefereeDisplayName,
    string Status,
    DateTime RegisteredAtUtc,
    DateTime? QualifiedAtUtc,
    DateTime? RewardedAtUtc,
    /// <summary>The referrer's reward value for this referral - null while not yet Rewarded, or if the per-referrer cap skipped this referral's referrer-side reward.</summary>
    decimal? RewardEarned);

// ---- Admin config (admin-api) ----

public record ProviderReferralProgramConfigResponse(
    Guid Id,
    decimal ReferrerRewardValue,
    decimal RefereeRewardValue,
    int QualifyingCompletedJobsCount,
    int ReferralExpiryDays,
    int? MaxReferralsPerProvider,
    bool IsActive,
    DateTime UpdatedAtUtc);

public record ProviderReferralProgramConfigUpdateRequest(
    decimal ReferrerRewardValue,
    decimal RefereeRewardValue,
    int QualifyingCompletedJobsCount,
    int ReferralExpiryDays,
    int? MaxReferralsPerProvider,
    bool IsActive);

// ---- Admin list/detail/fraud review (admin-api) ----

public record ProviderReferralAdminSearchRequest(
    ProviderReferralStatus? Status,
    bool? IsFraudFlagged,
    string? ProviderSearch,
    int Page = 1,
    int PageSize = 20);

public record ProviderReferralAdminListItemResponse(
    Guid Id,
    Guid ReferrerProviderId,
    string ReferrerName,
    Guid RefereeProviderId,
    string RefereeName,
    ProviderReferralStatus Status,
    bool IsFraudFlagged,
    DateTime RegisteredAtUtc,
    DateTime? RewardedAtUtc);

public record ProviderReferralAdminSearchResponse(
    IReadOnlyList<ProviderReferralAdminListItemResponse> Items, int TotalCount, int Page, int PageSize);

public record ProviderReferralAdminDetailResponse(
    Guid Id,
    Guid ReferrerProviderId,
    string ReferrerName,
    string ReferrerPhone,
    Guid RefereeProviderId,
    string RefereeName,
    string RefereePhone,
    string ReferralCodeUsed,
    ProviderReferralStatus Status,
    Guid? QualifyingBookingId,
    decimal ReferrerRewardValue,
    decimal RefereeRewardValue,
    int QualifyingCompletedJobsCount,
    Guid? ReferrerEarningEntryId,
    Guid? RefereeEarningEntryId,
    DateTime RegisteredAtUtc,
    DateTime? QualifiedAtUtc,
    DateTime? RewardedAtUtc,
    DateTime ExpiresAtUtc,
    bool IsFraudFlagged,
    string? FraudReviewNote,
    Guid? FraudReviewedByAdminUserId,
    DateTime? FraudReviewedAtUtc);

/// <summary>Fraud review queue actions - an optional admin note, mirrors ReferralFraudReviewRequest.</summary>
public record ProviderReferralFraudReviewRequest(string? Note);

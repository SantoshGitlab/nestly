using Nestly.Domain;

namespace Nestly.Application.Referral;

/// <summary>Admin view of the single referral program config row (task 167, mirrors CouponAdminResponse's shape).</summary>
public record ReferralProgramConfigResponse(
    Guid Id,
    ReferralRewardType ReferrerRewardType,
    decimal ReferrerRewardValue,
    ReferralRewardType RefereeRewardType,
    decimal RefereeRewardValue,
    decimal MinQualifyingOrderAmount,
    int ReferralExpiryDays,
    int? MaxReferralsPerCustomer,
    bool IsActive,
    DateTime UpdatedAtUtc);

/// <summary>Admin edit request for every mutable field of the referral program config (task 167).</summary>
public record ReferralProgramConfigUpdateRequest(
    ReferralRewardType ReferrerRewardType,
    decimal ReferrerRewardValue,
    ReferralRewardType RefereeRewardType,
    decimal RefereeRewardValue,
    decimal MinQualifyingOrderAmount,
    int ReferralExpiryDays,
    int? MaxReferralsPerCustomer,
    bool IsActive);

/// <summary>Admin view of a referral milestone (task 174's mechanism, exposed for admin configuration since no task in this phase asks for a dedicated milestone UI beyond simple create/list/toggle).</summary>
public record ReferralMilestoneResponse(
    Guid Id,
    int ThresholdCount,
    ReferralRewardType BonusType,
    decimal BonusValue,
    bool IsActive,
    DateTime CreatedAtUtc);

public record ReferralMilestoneCreateRequest(
    int ThresholdCount,
    ReferralRewardType BonusType,
    decimal BonusValue);

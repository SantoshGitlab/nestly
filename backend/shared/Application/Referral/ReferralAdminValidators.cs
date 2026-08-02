using FluentValidation;

namespace Nestly.Application.Referral;

/// <summary>Task 167's admin config edit form.</summary>
public class ReferralProgramConfigUpdateRequestValidator : AbstractValidator<ReferralProgramConfigUpdateRequest>
{
    public ReferralProgramConfigUpdateRequestValidator()
    {
        RuleFor(x => x.ReferrerRewardValue).GreaterThan(0);
        RuleFor(x => x.RefereeRewardValue).GreaterThan(0);
        RuleFor(x => x.MinQualifyingOrderAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ReferralExpiryDays).GreaterThan(0);
        RuleFor(x => x.MaxReferralsPerCustomer).GreaterThan(0).When(x => x.MaxReferralsPerCustomer.HasValue);
    }
}

/// <summary>Task 174's milestone tier create form.</summary>
public class ReferralMilestoneCreateRequestValidator : AbstractValidator<ReferralMilestoneCreateRequest>
{
    public ReferralMilestoneCreateRequestValidator()
    {
        RuleFor(x => x.ThresholdCount).GreaterThan(0);
        RuleFor(x => x.BonusValue).GreaterThan(0);
    }
}

/// <summary>Task 170's admin referral search/filter form.</summary>
public class ReferralAdminSearchRequestValidator : AbstractValidator<ReferralAdminSearchRequest>
{
    public ReferralAdminSearchRequestValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

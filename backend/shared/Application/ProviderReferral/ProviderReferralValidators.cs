using FluentValidation;

namespace Nestly.Application.ProviderReferral;

public class ProviderReferralProgramConfigUpdateRequestValidator : AbstractValidator<ProviderReferralProgramConfigUpdateRequest>
{
    public ProviderReferralProgramConfigUpdateRequestValidator()
    {
        RuleFor(x => x.ReferrerRewardValue).GreaterThan(0);
        RuleFor(x => x.RefereeRewardValue).GreaterThan(0);
        RuleFor(x => x.QualifyingCompletedJobsCount).GreaterThan(0);
        RuleFor(x => x.ReferralExpiryDays).GreaterThan(0);
        RuleFor(x => x.MaxReferralsPerProvider).GreaterThan(0).When(x => x.MaxReferralsPerProvider.HasValue);
    }
}

public class ProviderReferralAdminSearchRequestValidator : AbstractValidator<ProviderReferralAdminSearchRequest>
{
    public ProviderReferralAdminSearchRequestValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

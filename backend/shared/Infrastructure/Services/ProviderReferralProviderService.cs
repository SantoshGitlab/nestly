using Nestly.Application;
using Nestly.Application.ProviderReferral;
using Nestly.Domain;

namespace Nestly.Infrastructure.Services;

/// <summary>See <see cref="IProviderReferralProviderService"/>. Mirrors ReferralCustomerService.</summary>
public class ProviderReferralProviderService : IProviderReferralProviderService
{
    private readonly IProviderReferralRepository _referralRepository;
    private readonly IProviderReferralCodeService _codeService;
    private readonly IProviderRepository _providerRepository;

    public ProviderReferralProviderService(
        IProviderReferralRepository referralRepository,
        IProviderReferralCodeService codeService,
        IProviderRepository providerRepository)
    {
        _referralRepository = referralRepository;
        _codeService = codeService;
        _providerRepository = providerRepository;
    }

    public async Task<ProviderReferralSummaryResponse> GetSummaryAsync(Guid providerId)
    {
        string code = await _codeService.GetOrCreateCodeAsync(providerId);
        string shareLink = _codeService.BuildShareLink(code);

        var referrals = await _referralRepository.ListByReferrerProviderIdAsync(providerId);

        int invited = referrals.Count;
        int qualified = referrals.Count(r => r.Status is ProviderReferralStatus.Qualified or ProviderReferralStatus.Rewarded);
        int rewarded = referrals.Count(r => r.Status == ProviderReferralStatus.Rewarded);
        decimal totalEarned = referrals
            .Where(r => r.Status == ProviderReferralStatus.Rewarded && r.ReferrerEarningEntryId != null)
            .Sum(r => r.ReferrerRewardValue);

        return new ProviderReferralSummaryResponse(code, shareLink, invited, qualified, rewarded, totalEarned);
    }

    public async Task<IReadOnlyList<ProviderReferralHistoryItemResponse>> GetHistoryAsync(Guid providerId)
    {
        var referrals = await _referralRepository.ListByReferrerProviderIdAsync(providerId);

        var refereeNames = await _providerRepository.GetDisplayNamesByIdsAsync(
            referrals.Select(r => r.RefereeProviderId).Distinct().ToList());

        var items = new List<ProviderReferralHistoryItemResponse>(referrals.Count);

        foreach (var referral in referrals)
        {
            decimal? rewardEarned = referral.Status == ProviderReferralStatus.Rewarded && referral.ReferrerEarningEntryId is null
                ? null
                : referral.Status == ProviderReferralStatus.Rewarded ? referral.ReferrerRewardValue : null;

            items.Add(new ProviderReferralHistoryItemResponse(
                referral.Id,
                refereeNames.GetValueOrDefault(referral.RefereeProviderId, "Nestly provider"),
                referral.Status.ToString(),
                referral.RegisteredAtUtc,
                referral.QualifiedAtUtc,
                referral.RewardedAtUtc,
                rewardEarned));
        }

        return items;
    }
}

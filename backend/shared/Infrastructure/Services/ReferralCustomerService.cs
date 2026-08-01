using Nestly.Application;
using Nestly.Application.Referral;
using Nestly.Domain;

namespace Nestly.Infrastructure.Services;

/// <summary>See <see cref="IReferralCustomerService"/>.</summary>
public class ReferralCustomerService : IReferralCustomerService
{
    private readonly IReferralRepository _referralRepository;
    private readonly IReferralCodeService _codeService;
    private readonly ICustomerRepository _customerRepository;

    public ReferralCustomerService(
        IReferralRepository referralRepository,
        IReferralCodeService codeService,
        ICustomerRepository customerRepository)
    {
        _referralRepository = referralRepository;
        _codeService = codeService;
        _customerRepository = customerRepository;
    }

    public async Task<ReferralSummaryResponse> GetSummaryAsync(Guid customerId)
    {
        string code = await _codeService.GetOrCreateCodeAsync(customerId);
        string shareLink = _codeService.BuildShareLink(code);

        var referrals = await _referralRepository.ListByReferrerCustomerIdAsync(customerId);

        int invited = referrals.Count;
        int qualified = referrals.Count(r => r.Status is ReferralStatus.Qualified or ReferralStatus.Rewarded);
        int rewarded = referrals.Count(r => r.Status == ReferralStatus.Rewarded);
        decimal totalEarned = referrals
            .Where(r => r.Status == ReferralStatus.Rewarded && (r.ReferrerWalletEntryId != null || r.ReferrerCouponId != null))
            .Sum(r => r.ReferrerRewardValue);

        return new ReferralSummaryResponse(code, shareLink, invited, qualified, rewarded, totalEarned);
    }

    public async Task<IReadOnlyList<ReferralHistoryItemResponse>> GetHistoryAsync(Guid customerId)
    {
        var referrals = await _referralRepository.ListByReferrerCustomerIdAsync(customerId);
        var items = new List<ReferralHistoryItemResponse>(referrals.Count);

        foreach (var referral in referrals)
        {
            Customer? referee = await _customerRepository.GetByIdAsync(referral.RefereeCustomerId);
            decimal? rewardEarned = referral.Status == ReferralStatus.Rewarded && referral.ReferrerWalletEntryId is null && referral.ReferrerCouponId is null
                ? null
                : referral.Status == ReferralStatus.Rewarded ? referral.ReferrerRewardValue : null;

            items.Add(new ReferralHistoryItemResponse(
                referral.Id,
                referee?.Name ?? "Nestly customer",
                referral.Status.ToString(),
                referral.RegisteredAtUtc,
                referral.QualifiedAtUtc,
                referral.RewardedAtUtc,
                rewardEarned));
        }

        return items;
    }
}

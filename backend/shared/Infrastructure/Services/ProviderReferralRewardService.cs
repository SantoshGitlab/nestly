using Microsoft.Extensions.Logging;
using Nestly.Application;
using Nestly.Application.ProviderManagement;
using Nestly.Application.ProviderReferral;
using Nestly.Domain;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// See <see cref="IProviderReferralRewardService"/>. Mirrors
/// ReferralRewardService's shape, but both sides are always credited to the
/// provider earning ledger (<see cref="IProviderEarningLedgerService"/>) -
/// there is no coupon-reward option for providers, see
/// <see cref="ProviderReferralProgramConfig"/>'s doc comment.
/// </summary>
public class ProviderReferralRewardService : IProviderReferralRewardService
{
    private readonly IProviderReferralRepository _referralRepository;
    private readonly IProviderReferralProgramConfigRepository _configRepository;
    private readonly IProviderRepository _providerRepository;
    private readonly IProviderEarningLedgerService _earningLedgerService;
    private readonly ILogger<ProviderReferralRewardService> _logger;

    public ProviderReferralRewardService(
        IProviderReferralRepository referralRepository,
        IProviderReferralProgramConfigRepository configRepository,
        IProviderRepository providerRepository,
        IProviderEarningLedgerService earningLedgerService,
        ILogger<ProviderReferralRewardService> logger)
    {
        _referralRepository = referralRepository;
        _configRepository = configRepository;
        _providerRepository = providerRepository;
        _earningLedgerService = earningLedgerService;
        _logger = logger;
    }

    public async Task DisburseAsync(ProviderReferral referral)
    {
        bool referrerExists = await _providerRepository.ExistsAsync(referral.ReferrerProviderId);
        bool refereeExists = await _providerRepository.ExistsAsync(referral.RefereeProviderId);
        if (!referrerExists || !refereeExists)
        {
            _logger.LogWarning("Provider referral {ReferralId} could not be disbursed: referrer or referee no longer exists.", referral.Id);
            return;
        }

        // Per-provider reward cap (PROVIDER-REFERRAL.md "FRAUD / ABUSE
        // PREVENTION"): caps how many times the REFERRER can be rewarded, not
        // the referee - the referee is only ever rewarded once, for their own
        // qualification, regardless of how many people their referrer refers.
        ProviderReferralProgramConfig? config = await _configRepository.GetAsync();
        int? cap = config?.MaxReferralsPerProvider;
        bool referrerCapReached = cap is not null
            && await _referralRepository.CountRewardedByReferrerAsync(referral.ReferrerProviderId) >= cap;

        Guid? referrerEarningEntryId = null;
        if (!referrerCapReached)
        {
            var referrerResult = await _earningLedgerService.RecordAdjustmentAsync(
                referral.ReferrerProviderId,
                new RecordProviderEarningAdjustmentRequest(
                    ProviderEarningEntryType.Credit, referral.ReferrerRewardValue,
                    ProviderEarningSourceType.ProviderReferralReward, referral.Id, "Provider referral reward"));

            if (referrerResult.IsSuccess)
            {
                referrerEarningEntryId = referrerResult.Value.Id;
            }
            else
            {
                _logger.LogError(
                    "Provider referral {ReferralId}: failed to credit referrer {ReferrerId}'s reward - {Error}.",
                    referral.Id, referral.ReferrerProviderId, referrerResult.Error.Message);
            }
        }
        else
        {
            _logger.LogInformation(
                "Provider referral {ReferralId}: referrer {ReferrerId} has reached the {Cap}-referral reward cap - referrer side skipped, referee still rewarded.",
                referral.Id, referral.ReferrerProviderId, cap);
        }

        Guid? refereeEarningEntryId = null;
        var refereeResult = await _earningLedgerService.RecordAdjustmentAsync(
            referral.RefereeProviderId,
            new RecordProviderEarningAdjustmentRequest(
                ProviderEarningEntryType.Credit, referral.RefereeRewardValue,
                ProviderEarningSourceType.ProviderReferralReward, referral.Id, "Provider referral reward"));

        if (refereeResult.IsSuccess)
        {
            refereeEarningEntryId = refereeResult.Value.Id;
        }
        else
        {
            _logger.LogError(
                "Provider referral {ReferralId}: failed to credit referee {RefereeId}'s reward - {Error}.",
                referral.Id, referral.RefereeProviderId, refereeResult.Error.Message);
        }

        referral.MarkRewarded(referrerEarningEntryId, refereeEarningEntryId);
        await _referralRepository.UpdateAsync(referral);

        // No notification dispatch here, by design: NotificationEvent.CustomerId
        // is a required FK to the customer table (see
        // CustomerRegistrationService's welcome-notification doc comment for
        // the same constraint), so it cannot record a provider recipient
        // without a schema change - out of scope for this pass. The reward
        // is visible to both providers via GET /me/referral/history.
    }
}

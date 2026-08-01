using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Nestly.Application;
using Nestly.Application.Coupons;
using Nestly.Application.Notifications;
using Nestly.Application.Referral;
using Nestly.Application.Wallet;
using Nestly.Domain;

namespace Nestly.Infrastructure.Services;

/// <summary>See <see cref="IReferralRewardService"/>.</summary>
public class ReferralRewardService : IReferralRewardService
{
    // Referral-issued coupons are single-use-per-recipient and don't need a
    // human-memorable code (the recipient never types it in - RestrictedToCustomerId
    // is what actually gates redemption, the code just has to be unique).
    private const string CodeAlphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";
    private const int CodeSuffixLength = 10;
    private const int MaxCodeGenerationAttempts = 10;

    // No expiry configuration exists yet for referral-issued coupons
    // specifically (REFERRAL.md doesn't specify one) - 90 days is a
    // reasonable, generous default matching typical promotional-coupon
    // lifetimes elsewhere in this codebase, not a value read from
    // ReferralProgramConfig (which governs the referral itself, not a
    // reward coupon's own shelf life).
    private static readonly TimeSpan CouponValidityWindow = TimeSpan.FromDays(90);

    private readonly IReferralRepository _referralRepository;
    private readonly IReferralProgramConfigRepository _configRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IWalletService _walletService;
    private readonly ICouponRepository _couponRepository;
    private readonly INotificationDispatchService _notificationDispatchService;
    private readonly ILogger<ReferralRewardService> _logger;

    public ReferralRewardService(
        IReferralRepository referralRepository,
        IReferralProgramConfigRepository configRepository,
        ICustomerRepository customerRepository,
        IWalletService walletService,
        ICouponRepository couponRepository,
        INotificationDispatchService notificationDispatchService,
        ILogger<ReferralRewardService> logger)
    {
        _referralRepository = referralRepository;
        _configRepository = configRepository;
        _customerRepository = customerRepository;
        _walletService = walletService;
        _couponRepository = couponRepository;
        _notificationDispatchService = notificationDispatchService;
        _logger = logger;
    }

    public async Task DisburseAsync(Referral referral)
    {
        Customer? referrer = await _customerRepository.GetByIdAsync(referral.ReferrerCustomerId);
        Customer? referee = await _customerRepository.GetByIdAsync(referral.RefereeCustomerId);
        if (referrer is null || referee is null)
        {
            _logger.LogWarning("Referral {ReferralId} could not be disbursed: referrer or referee no longer exists.", referral.Id);
            return;
        }

        // Per-customer reward cap (REFERRAL.md "FRAUD / ABUSE PREVENTION"):
        // caps how many times the REFERRER can be rewarded, not the
        // referee - the referee is only ever rewarded once, for their own
        // qualifying booking, regardless of how many people their referrer
        // has referred.
        ReferralProgramConfig? config = await _configRepository.GetAsync();
        int? cap = config?.MaxReferralsPerCustomer;
        bool referrerCapReached = cap is not null
            && await _referralRepository.CountRewardedByReferrerAsync(referral.ReferrerCustomerId) >= cap;

        Guid? referrerWalletEntryId = null;
        Guid? referrerCouponId = null;
        if (!referrerCapReached)
        {
            (referrerWalletEntryId, referrerCouponId) = await IssueRewardAsync(
                referrer, referral.ReferrerRewardType, referral.ReferrerRewardValue, referral.Id);
        }
        else
        {
            _logger.LogInformation(
                "Referral {ReferralId}: referrer {ReferrerId} has reached the {Cap}-referral reward cap - referrer side skipped, referee still rewarded.",
                referral.Id, referral.ReferrerCustomerId, cap);
        }

        (Guid? refereeWalletEntryId, Guid? refereeCouponId) = await IssueRewardAsync(
            referee, referral.RefereeRewardType, referral.RefereeRewardValue, referral.Id);

        referral.MarkRewarded(referrerWalletEntryId, referrerCouponId, refereeWalletEntryId, refereeCouponId);
        await _referralRepository.UpdateAsync(referral);

        if (!referrerCapReached)
        {
            await _notificationDispatchService.DispatchAsync(
                referrer.Id,
                NotificationEventType.ReferralRewardCredited,
                new NotificationRecipient(referrer.Mobile, referrer.Email),
                new Dictionary<string, string> { ["RewardValue"] = referral.ReferrerRewardValue.ToString("0.00") });
        }

        await _notificationDispatchService.DispatchAsync(
            referee.Id,
            NotificationEventType.ReferralRewardCredited,
            new NotificationRecipient(referee.Mobile, referee.Email),
            new Dictionary<string, string> { ["RewardValue"] = referral.RefereeRewardValue.ToString("0.00") });
    }

    private async Task<(Guid? WalletEntryId, Guid? CouponId)> IssueRewardAsync(
        Customer recipient, ReferralRewardType rewardType, decimal rewardValue, Guid referralId)
    {
        if (rewardType == ReferralRewardType.WalletCredit)
        {
            var entry = await _walletService.CreditAsync(
                recipient.Id, rewardValue, WalletSourceType.ReferralReward, referralId, "Referral reward");
            return (entry.Id, null);
        }

        var nowUtc = DateTime.UtcNow;
        var coupon = new Coupon(
            Guid.NewGuid(),
            await GenerateUniqueCouponCodeAsync(),
            "Referral reward",
            CouponDiscountType.Flat,
            rewardValue,
            maxDiscountAmount: null,
            minOrderAmount: 0,
            validFromUtc: nowUtc,
            validToUtc: nowUtc.Add(CouponValidityWindow),
            usageLimitTotal: 1,
            usageLimitPerCustomer: 1,
            applicableCategoryId: null,
            customerSegment: CouponCustomerSegment.All);
        coupon.RestrictToCustomer(recipient.Id);

        await _couponRepository.AddAsync(coupon);
        return (null, coupon.Id);
    }

    private async Task<string> GenerateUniqueCouponCodeAsync()
    {
        for (int attempt = 0; attempt < MaxCodeGenerationAttempts; attempt++)
        {
            string candidate = $"REF-{GenerateSuffix()}";
            if (!await _couponRepository.CodeExistsAsync(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException($"Could not generate a unique referral coupon code after {MaxCodeGenerationAttempts} attempts.");
    }

    private static string GenerateSuffix()
    {
        var chars = new char[CodeSuffixLength];
        for (int i = 0; i < CodeSuffixLength; i++)
        {
            chars[i] = CodeAlphabet[RandomNumberGenerator.GetInt32(0, CodeAlphabet.Length)];
        }

        return new string(chars);
    }
}

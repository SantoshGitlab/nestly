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

    // Task 175: referral wallet-credit rewards (per-referral and milestone
    // bonus alike) expire if unspent, so the program's cash liability doesn't
    // sit on the books indefinitely. Same "no dedicated config field yet"
    // reasoning as CouponValidityWindow above - a year is a generous default.
    private static readonly TimeSpan ReferralCreditExpiryWindow = TimeSpan.FromDays(365);

    private readonly IReferralRepository _referralRepository;
    private readonly IReferralProgramConfigRepository _configRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IWalletService _walletService;
    private readonly ICouponRepository _couponRepository;
    private readonly IReferralMilestoneRepository _milestoneRepository;
    private readonly IReferralMilestoneAwardRepository _milestoneAwardRepository;
    private readonly INotificationDispatchService _notificationDispatchService;
    private readonly ILogger<ReferralRewardService> _logger;

    public ReferralRewardService(
        IReferralRepository referralRepository,
        IReferralProgramConfigRepository configRepository,
        ICustomerRepository customerRepository,
        IWalletService walletService,
        ICouponRepository couponRepository,
        IReferralMilestoneRepository milestoneRepository,
        IReferralMilestoneAwardRepository milestoneAwardRepository,
        INotificationDispatchService notificationDispatchService,
        ILogger<ReferralRewardService> logger)
    {
        _referralRepository = referralRepository;
        _configRepository = configRepository;
        _customerRepository = customerRepository;
        _walletService = walletService;
        _couponRepository = couponRepository;
        _milestoneRepository = milestoneRepository;
        _milestoneAwardRepository = milestoneAwardRepository;
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
                referrer, referral.ReferrerRewardType, referral.ReferrerRewardValue,
                WalletSourceType.ReferralReward, referral.Id, "Referral reward");
        }
        else
        {
            _logger.LogInformation(
                "Referral {ReferralId}: referrer {ReferrerId} has reached the {Cap}-referral reward cap - referrer side skipped, referee still rewarded.",
                referral.Id, referral.ReferrerCustomerId, cap);
        }

        (Guid? refereeWalletEntryId, Guid? refereeCouponId) = await IssueRewardAsync(
            referee, referral.RefereeRewardType, referral.RefereeRewardValue,
            WalletSourceType.ReferralReward, referral.Id, "Referral reward");

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

        // Task 174: milestone bonus, layered on top of the per-referral
        // reward just disbursed above - checked on the referrer's REWARDED
        // count regardless of the per-referral cap (that cap governs the
        // per-referral reward only; the referral itself still counts toward
        // a milestone even on a capped referral).
        await CheckAndAwardMilestoneAsync(referrer);
    }

    private async Task CheckAndAwardMilestoneAsync(Customer referrer)
    {
        int rewardedCount = await _referralRepository.CountRewardedByReferrerAsync(referrer.Id);
        var milestones = await _milestoneRepository.ListActiveOrderedByThresholdAsync();
        var milestone = milestones.FirstOrDefault(m => m.ThresholdCount == rewardedCount);
        if (milestone is null)
        {
            return;
        }

        if (await _milestoneAwardRepository.ExistsAsync(milestone.Id, referrer.Id))
        {
            // Already paid (e.g. a retried handler invocation) - the award
            // row is the idempotency guard, never pay the same milestone twice.
            return;
        }

        (Guid? walletEntryId, Guid? couponId) = await IssueRewardAsync(
            referrer, milestone.BonusType, milestone.BonusValue,
            WalletSourceType.ReferralMilestoneBonus, milestone.Id, "Referral milestone bonus");

        await _milestoneAwardRepository.AddAsync(
            new ReferralMilestoneAward(Guid.NewGuid(), milestone.Id, referrer.Id, walletEntryId, couponId));

        await _notificationDispatchService.DispatchAsync(
            referrer.Id,
            NotificationEventType.ReferralRewardCredited,
            new NotificationRecipient(referrer.Mobile, referrer.Email),
            new Dictionary<string, string> { ["RewardValue"] = milestone.BonusValue.ToString("0.00") });
    }

    private async Task<(Guid? WalletEntryId, Guid? CouponId)> IssueRewardAsync(
        Customer recipient, ReferralRewardType rewardType, decimal rewardValue,
        WalletSourceType walletSourceType, Guid sourceReferenceId, string description)
    {
        if (rewardType == ReferralRewardType.WalletCredit)
        {
            var entry = await _walletService.CreditAsync(
                recipient.Id, rewardValue, walletSourceType, sourceReferenceId, description,
                expiresAtUtc: DateTime.UtcNow.Add(ReferralCreditExpiryWindow));
            return (entry.Id, null);
        }

        var nowUtc = DateTime.UtcNow;
        var coupon = new Coupon(
            Guid.NewGuid(),
            await GenerateUniqueCouponCodeAsync(),
            description,
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

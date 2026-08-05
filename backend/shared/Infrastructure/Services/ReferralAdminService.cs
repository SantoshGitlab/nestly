using Nestly.Application;
using Nestly.Application.Customers;
using Nestly.Application.Referral;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;

namespace Nestly.Infrastructure.Services;

/// <summary>See <see cref="IReferralAdminService"/>.</summary>
public class ReferralAdminService : IReferralAdminService
{
    // Cap on how many customers a single name/mobile/email search term can
    // resolve to before being used as a referral filter - the admin search
    // box is meant for narrowing to one or a handful of people, not browsing
    // the whole customer base through it.
    private const int CustomerSearchLookupCap = 100;

    private readonly IReferralRepository _referralRepository;
    private readonly IReferralMilestoneRepository _milestoneRepository;
    private readonly IReferralMilestoneAwardRepository _milestoneAwardRepository;
    private readonly ICustomerRepository _customerRepository;

    public ReferralAdminService(
        IReferralRepository referralRepository,
        IReferralMilestoneRepository milestoneRepository,
        IReferralMilestoneAwardRepository milestoneAwardRepository,
        ICustomerRepository customerRepository)
    {
        _referralRepository = referralRepository;
        _milestoneRepository = milestoneRepository;
        _milestoneAwardRepository = milestoneAwardRepository;
        _customerRepository = customerRepository;
    }

    public async Task<ReferralAdminSearchResponse> SearchAsync(ReferralAdminSearchRequest request)
    {
        IReadOnlyList<Guid>? customerIds = null;
        if (!string.IsNullOrWhiteSpace(request.CustomerSearch))
        {
            customerIds = await ResolveCustomerIdsAsync(request.CustomerSearch);
            if (customerIds.Count == 0)
            {
                // No matching customer at all - short-circuit rather than
                // running an unfiltered referral search (which SearchAsync's
                // "customerIds is { Count: > 0 }" would otherwise do).
                return new ReferralAdminSearchResponse([], 0, request.Page, request.PageSize);
            }
        }

        var (items, totalCount) = await _referralRepository.SearchAsync(
            request.Status, request.IsFraudFlagged, customerIds, request.Page, request.PageSize);

        // Task 253: this used to issue two GetByIdAsync calls per row - a
        // 100-row page cost 200 sequential round trips, each loading a whole
        // Customer aggregate to read one column. One batched name lookup
        // covers both sides of every referral on the page.
        var names = await _customerRepository.GetNamesByIdsAsync(
            items.SelectMany(r => new[] { r.ReferrerCustomerId, r.RefereeCustomerId })
                 .Distinct()
                 .ToList());

        var responses = new List<ReferralAdminListItemResponse>(items.Count);
        foreach (var referral in items)
        {
            responses.Add(new ReferralAdminListItemResponse(
                referral.Id,
                referral.ReferrerCustomerId,
                names.GetValueOrDefault(referral.ReferrerCustomerId, "Unknown"),
                referral.RefereeCustomerId,
                names.GetValueOrDefault(referral.RefereeCustomerId, "Unknown"),
                referral.Status,
                referral.IsFraudFlagged,
                referral.RegisteredAtUtc,
                referral.RewardedAtUtc));
        }

        return new ReferralAdminSearchResponse(responses, totalCount, request.Page, request.PageSize);
    }

    public async Task<Result<ReferralAdminDetailResponse>> GetByIdAsync(Guid id)
    {
        var referral = await _referralRepository.GetByIdAsync(id);
        if (referral is null)
        {
            return Error.NotFound("Referral.NotFound", "This referral does not exist.");
        }

        var referrer = await _customerRepository.GetByIdAsync(referral.ReferrerCustomerId);
        var referee = await _customerRepository.GetByIdAsync(referral.RefereeCustomerId);

        return Result.Success(new ReferralAdminDetailResponse(
            referral.Id,
            referral.ReferrerCustomerId,
            referrer?.Name ?? "Unknown",
            referrer?.Mobile ?? string.Empty,
            referral.RefereeCustomerId,
            referee?.Name ?? "Unknown",
            referee?.Mobile ?? string.Empty,
            referral.ReferralCodeUsed,
            referral.Status,
            referral.QualifyingBookingId,
            referral.ReferrerRewardType,
            referral.ReferrerRewardValue,
            referral.RefereeRewardType,
            referral.RefereeRewardValue,
            referral.MinQualifyingOrderAmount,
            referral.ReferrerWalletEntryId,
            referral.ReferrerCouponId,
            referral.RefereeWalletEntryId,
            referral.RefereeCouponId,
            referral.RegisteredAtUtc,
            referral.QualifiedAtUtc,
            referral.RewardedAtUtc,
            referral.ExpiresAtUtc,
            referral.IsFraudFlagged,
            referral.FraudReviewNote,
            referral.FraudReviewedByAdminUserId,
            referral.FraudReviewedAtUtc));
    }

    public async Task<Result<ReferralFunnelReportResponse>> GetFunnelReportAsync(DateTime? fromUtc, DateTime? toUtc)
    {
        if (fromUtc.HasValue && toUtc.HasValue && fromUtc > toUtc)
        {
            return Error.Validation("ReferralReport.InvalidRange", "fromUtc must not be after toUtc.");
        }

        var cohort = await _referralRepository.ListRegisteredInRangeAsync(fromUtc, toUtc);
        int registered = cohort.Count;
        int qualified = cohort.Count(r => r.Status is ReferralStatus.Qualified or ReferralStatus.Rewarded);
        int rewarded = cohort.Count(r => r.Status == ReferralStatus.Rewarded);

        return Result.Success(new ReferralFunnelReportResponse(registered, registered, qualified, rewarded, fromUtc, toUtc));
    }

    public async Task<Result<ReferralCostReportResponse>> GetCostReportAsync(DateTime? fromUtc, DateTime? toUtc)
    {
        if (fromUtc.HasValue && toUtc.HasValue && fromUtc > toUtc)
        {
            return Error.Validation("ReferralReport.InvalidRange", "fromUtc must not be after toUtc.");
        }

        var rewarded = await _referralRepository.ListRewardedInRangeAsync(fromUtc, toUtc);

        decimal walletCost = 0m;
        decimal couponCost = 0m;
        foreach (var referral in rewarded)
        {
            if (referral.ReferrerWalletEntryId is not null)
            {
                walletCost += referral.ReferrerRewardValue;
            }
            else if (referral.ReferrerCouponId is not null)
            {
                couponCost += referral.ReferrerRewardValue;
            }

            if (referral.RefereeWalletEntryId is not null)
            {
                walletCost += referral.RefereeRewardValue;
            }
            else if (referral.RefereeCouponId is not null)
            {
                couponCost += referral.RefereeRewardValue;
            }
        }

        var milestoneAwards = await _milestoneAwardRepository.ListInRangeAsync(fromUtc, toUtc);
        if (milestoneAwards.Count > 0)
        {
            // The award row doesn't carry the bonus amount itself (that
            // lives on the milestone it references) - one lookup of the
            // whole (small, admin-managed) milestone table beats N+1 queries.
            var milestonesById = (await _milestoneRepository.ListAllOrderedByThresholdAsync())
                .ToDictionary(m => m.Id);

            foreach (var award in milestoneAwards)
            {
                if (!milestonesById.TryGetValue(award.ReferralMilestoneId, out var milestone))
                {
                    continue;
                }

                if (award.WalletEntryId is not null)
                {
                    walletCost += milestone.BonusValue;
                }
                else if (award.CouponId is not null)
                {
                    couponCost += milestone.BonusValue;
                }
            }
        }

        return Result.Success(new ReferralCostReportResponse(
            walletCost, couponCost, walletCost + couponCost, rewarded.Count, milestoneAwards.Count, fromUtc, toUtc));
    }

    private async Task<IReadOnlyList<Guid>> ResolveCustomerIdsAsync(string searchTerm)
    {
        var ids = new HashSet<Guid>();

        var byName = await _customerRepository.SearchAsync(new CustomerSearchFilter(
            searchTerm, null, null, null, null, null, null, null, null, 1, CustomerSearchLookupCap));
        foreach (var row in byName.Rows) ids.Add(row.Customer.Id);

        var byMobile = await _customerRepository.SearchAsync(new CustomerSearchFilter(
            null, searchTerm, null, null, null, null, null, null, null, 1, CustomerSearchLookupCap));
        foreach (var row in byMobile.Rows) ids.Add(row.Customer.Id);

        var byEmail = await _customerRepository.SearchAsync(new CustomerSearchFilter(
            null, null, searchTerm, null, null, null, null, null, null, 1, CustomerSearchLookupCap));
        foreach (var row in byEmail.Rows) ids.Add(row.Customer.Id);

        return ids.ToList();
    }
}

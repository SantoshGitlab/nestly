using Nestly.Application;
using Nestly.Application.ProviderManagement;
using Nestly.Application.ProviderReferral;
using Nestly.BuildingBlocks.Results;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// See <see cref="IProviderReferralAdminService"/>. Mirrors ReferralAdminService
/// (funnel/cost reports intentionally not included in this v1).
/// </summary>
public class ProviderReferralAdminService : IProviderReferralAdminService
{
    // Cap on how many providers a single name/phone search term can resolve
    // to before being used as a referral filter, mirrors
    // ReferralAdminService.CustomerSearchLookupCap.
    private const int ProviderSearchLookupCap = 100;

    private readonly IProviderReferralRepository _referralRepository;
    private readonly IProviderRepository _providerRepository;

    public ProviderReferralAdminService(
        IProviderReferralRepository referralRepository,
        IProviderRepository providerRepository)
    {
        _referralRepository = referralRepository;
        _providerRepository = providerRepository;
    }

    public async Task<ProviderReferralAdminSearchResponse> SearchAsync(ProviderReferralAdminSearchRequest request)
    {
        IReadOnlyList<Guid>? providerIds = null;
        if (!string.IsNullOrWhiteSpace(request.ProviderSearch))
        {
            providerIds = await ResolveProviderIdsAsync(request.ProviderSearch);
            if (providerIds.Count == 0)
            {
                return new ProviderReferralAdminSearchResponse([], 0, request.Page, request.PageSize);
            }
        }

        var (items, totalCount) = await _referralRepository.SearchAsync(
            request.Status, request.IsFraudFlagged, providerIds, request.Page, request.PageSize);

        var names = await _providerRepository.GetDisplayNamesByIdsAsync(
            items.SelectMany(r => new[] { r.ReferrerProviderId, r.RefereeProviderId })
                 .Distinct()
                 .ToList());

        var responses = new List<ProviderReferralAdminListItemResponse>(items.Count);
        foreach (var referral in items)
        {
            responses.Add(new ProviderReferralAdminListItemResponse(
                referral.Id,
                referral.ReferrerProviderId,
                names.GetValueOrDefault(referral.ReferrerProviderId, "Unknown"),
                referral.RefereeProviderId,
                names.GetValueOrDefault(referral.RefereeProviderId, "Unknown"),
                referral.Status,
                referral.IsFraudFlagged,
                referral.RegisteredAtUtc,
                referral.RewardedAtUtc));
        }

        return new ProviderReferralAdminSearchResponse(responses, totalCount, request.Page, request.PageSize);
    }

    public async Task<Result<ProviderReferralAdminDetailResponse>> GetByIdAsync(Guid id)
    {
        var referral = await _referralRepository.GetByIdAsync(id);
        if (referral is null)
        {
            return Error.NotFound("ProviderReferral.NotFound", "This provider referral does not exist.");
        }

        var referrer = await _providerRepository.GetByIdAsync(referral.ReferrerProviderId);
        var referee = await _providerRepository.GetByIdAsync(referral.RefereeProviderId);

        return Result.Success(new ProviderReferralAdminDetailResponse(
            referral.Id,
            referral.ReferrerProviderId,
            referrer?.DisplayName ?? "Unknown",
            referrer?.Phone ?? string.Empty,
            referral.RefereeProviderId,
            referee?.DisplayName ?? "Unknown",
            referee?.Phone ?? string.Empty,
            referral.ReferralCodeUsed,
            referral.Status,
            referral.QualifyingBookingId,
            referral.ReferrerRewardValue,
            referral.RefereeRewardValue,
            referral.QualifyingCompletedJobsCount,
            referral.ReferrerEarningEntryId,
            referral.RefereeEarningEntryId,
            referral.RegisteredAtUtc,
            referral.QualifiedAtUtc,
            referral.RewardedAtUtc,
            referral.ExpiresAtUtc,
            referral.IsFraudFlagged,
            referral.FraudReviewNote,
            referral.FraudReviewedByAdminUserId,
            referral.FraudReviewedAtUtc));
    }

    private async Task<IReadOnlyList<Guid>> ResolveProviderIdsAsync(string searchTerm)
    {
        var ids = new HashSet<Guid>();

        var byName = await _providerRepository.SearchAsync(new ProviderSearchFilter(
            searchTerm, null, null, null, null, 1, ProviderSearchLookupCap));
        foreach (var row in byName.Rows) ids.Add(row.Id);

        var byPhone = await _providerRepository.SearchAsync(new ProviderSearchFilter(
            null, searchTerm, null, null, null, 1, ProviderSearchLookupCap));
        foreach (var row in byPhone.Rows) ids.Add(row.Id);

        return ids.ToList();
    }
}

using System.Runtime.CompilerServices;
using Nestly.Application.ProviderManagement;

namespace Nestly.Infrastructure.Services;

/// <summary>See <see cref="IEligibleProviderSearchService"/>.</summary>
public class EligibleProviderSearchService : IEligibleProviderSearchService
{
    private readonly IProviderMatchingService _matchingService;
    private readonly IProviderAssignmentEligibilityService _eligibilityService;

    public EligibleProviderSearchService(
        IProviderMatchingService matchingService,
        IProviderAssignmentEligibilityService eligibilityService)
    {
        _matchingService = matchingService;
        _eligibilityService = eligibilityService;
    }

    public async IAsyncEnumerable<ProviderMatchCandidate> FindEligibleAsync(
        Guid bookingId,
        IReadOnlyCollection<Guid>? excludeProviderIds = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var candidates = await _matchingService.FindCandidatesAsync(bookingId, excludeProviderIds, cancellationToken);

        foreach (var candidate in candidates)
        {
            if (await _eligibilityService.IsEligibleAsync(candidate.ProviderId, bookingId, cancellationToken))
            {
                yield return candidate;
            }
        }
    }
}

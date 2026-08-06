namespace Nestly.Application.ProviderManagement;

/// <summary>
/// Task 244: an active provider eligible for a booking (skill + service-area
/// matched), with the great-circle distance from the booking's address when
/// both sides have coordinates (task 243) - null when either is missing.
/// </summary>
public sealed record ProviderMatchCandidate(Guid ProviderId, decimal? DistanceKm);

/// <summary>
/// Ranks candidate providers for automatic assignment (PROVIDER.md OPEN
/// DECISIONS - AUTOMATIC ASSIGNMENT, task 242). Pure candidate discovery and
/// ranking - it does not check availability or capacity (task 245's job) and
/// does not write anything; task 246's orchestrator walks the ranked list
/// applying task 245's gate and assigns the first candidate that passes.
/// </summary>
public interface IProviderMatchingService
{
    /// <summary>
    /// Active providers whose <c>ProviderSkillMapping</c> covers the
    /// booking's category/service and whose <c>ProviderServiceArea</c>
    /// covers its city (and pincode, where an area is pincode-scoped),
    /// ordered nearest first. A provider with no coordinates set, or a
    /// booking address that resolved no coordinates, sorts after every
    /// candidate with a known distance rather than being excluded - it is
    /// still a legitimate candidate, just unranked by distance.
    /// <paramref name="excludeProviderIds"/> is task 247's rejection-retry
    /// exclusion list.
    /// </summary>
    Task<IReadOnlyList<ProviderMatchCandidate>> FindCandidatesAsync(Guid bookingId, IReadOnlyCollection<Guid>? excludeProviderIds = null);
}

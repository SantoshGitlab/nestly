namespace Nestly.Application.ProviderManagement;

/// <summary>
/// Task 244: an active provider eligible for a booking (skill + service-area
/// matched), ranked for automatic assignment.
/// </summary>
/// <param name="ProviderId">The candidate provider.</param>
/// <param name="DistanceKm">
/// Great-circle ("as the crow flies") kilometres from the booking's address
/// to the provider (task 243) - null when either end has no coordinates.
/// Unchanged by task 267: this is still air distance, never road distance, so
/// every existing reader keeps the units it was written against. Road
/// distance is deliberately not surfaced - nothing needs it, and a second
/// "distance" in different units on the same record would be a trap.
/// </param>
/// <param name="TravelDurationSeconds">
/// Task 267: real road travel time, in seconds, when this candidate was
/// ranked by it. Null when the candidate was ranked by
/// <paramref name="DistanceKm"/> instead - route ranking switched off, the
/// candidate outside the cost cap, or no real road data available - so a null
/// means "not ranked by travel time", never "instant".
/// </param>
public sealed record ProviderMatchCandidate(Guid ProviderId, decimal? DistanceKm, int? TravelDurationSeconds = null);

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
    /// ordered best-first: by real road travel time where task 267 could
    /// measure it, by great-circle distance otherwise. A provider with no
    /// coordinates set sorts after every candidate with a known distance
    /// rather than being excluded - it is still a legitimate candidate, just
    /// unranked by proximity. <paramref name="excludeProviderIds"/> is task
    /// 247's rejection-retry exclusion list.
    /// </summary>
    /// <remarks>
    /// Ranking never narrows the candidate set: every provider that passes
    /// the skill/service-area/status filters is returned, whatever its
    /// distance. Task 267's radius and candidate cap bound what is worth
    /// measuring, not who is eligible.
    /// </remarks>
    Task<IReadOnlyList<ProviderMatchCandidate>> FindCandidatesAsync(
        Guid bookingId,
        IReadOnlyCollection<Guid>? excludeProviderIds = null,
        CancellationToken cancellationToken = default);
}

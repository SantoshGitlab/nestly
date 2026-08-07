using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Nestly.Application.Bookings;
using Nestly.Application.ProviderManagement;
using Nestly.Application.Routing;
using Nestly.BuildingBlocks.Geo;
using Nestly.Domain;
using Nestly.Infrastructure.Options;
using Nestly.Infrastructure.Persistence;

namespace Nestly.Infrastructure.Services;

/// <summary>See <see cref="IProviderMatchingService"/>.</summary>
/// <remarks>
/// <para>
/// <b>Task 267 - ranking by road travel time.</b> Great-circle distance picks
/// the wrong provider whenever a river, a highway or a one-way system makes
/// the nearest-by-air candidate the furthest by road, so the cheap filters
/// (skill, service area, status) now cut the set first and the survivors are
/// priced with one batched <see cref="IRouteEstimateProvider"/> call and
/// ordered by duration.
/// </para>
/// <para>
/// <b>The direction approximation.</b> The provider drives to the customer,
/// so the true query is many origins to one destination. The batched routing
/// seam takes one origin and many destinations, so this class asks the mirror
/// question - from the booking's address to each candidate - and treats the
/// answer as the provider's inbound leg. Traffic is directional, so the two
/// are not the same number: a tidal-flow artery or a one-way system can make
/// the outbound leg materially slower than the inbound one. It is taken
/// deliberately, because the alternative is one HTTP round trip per candidate
/// instead of one per booking - the same billed element count but N times the
/// latency on a path that must not hold up a booking, no shared cache entry,
/// and no protection from the per-call element cap. The bias also applies
/// equally to every candidate (they all share one origin), so it distorts the
/// <i>ranking</i> only where one candidate's own asymmetry differs from its
/// peers', which is a far smaller error than the straight-line ordering this
/// replaces.
/// </para>
/// <para>
/// <b>Sandbox-only responses do not reorder anything.</b>
/// <see cref="IRouteEstimateProvider"/> never returns nothing - it degrades to
/// <see cref="SandboxRouteEstimateProvider"/>, whose estimate is the same
/// Haversine number scaled by a fixed winding factor and a fixed speed. That
/// is a strictly increasing function of the distance this class already has,
/// so ranking by it cannot be more accurate than ranking by distance, only
/// less: it is rounded to whole seconds, and two candidates metres apart
/// collapse into a tie that then resolves by <c>Provider.Id</c> - reordering
/// them for no reason. So when every estimate came back
/// <see cref="RouteEstimateSource.Sandbox"/>, the response carries no road
/// information and the straight-line ordering stands. That is what "the
/// provider returns nothing" means for this contract. A partially degraded
/// response is different and is still used: the sandbox figures in it are
/// comparable seconds-of-driving, and discarding real road data for every
/// candidate because one destination was unroutable would be the worse trade.
/// </para>
/// </remarks>
public class ProviderMatchingService : IProviderMatchingService
{
    private readonly IBookingRepository _bookingRepository;
    private readonly NestlyDbContext _context;
    private readonly IRouteEstimateProvider _routeEstimateProvider;
    private readonly IOptions<AutoAssignmentOptions> _options;

    public ProviderMatchingService(
        IBookingRepository bookingRepository,
        NestlyDbContext context,
        IRouteEstimateProvider routeEstimateProvider,
        IOptions<AutoAssignmentOptions> options)
    {
        _bookingRepository = bookingRepository;
        _context = context;
        _routeEstimateProvider = routeEstimateProvider;
        _options = options;
    }

    public async Task<IReadOnlyList<ProviderMatchCandidate>> FindCandidatesAsync(
        Guid bookingId,
        IReadOnlyCollection<Guid>? excludeProviderIds = null,
        CancellationToken cancellationToken = default)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId);
        if (booking is null || booking.Items.Count == 0)
        {
            return [];
        }

        var serviceId = booking.Items[0].ServiceId;
        var service = await _context.Set<Service>().AsNoTracking().FirstOrDefaultAsync(s => s.Id == serviceId, cancellationToken);
        var slotWindow = await _context.Set<SlotWindow>().AsNoTracking().FirstOrDefaultAsync(w => w.Id == booking.SlotWindowId, cancellationToken);
        if (service is null || slotWindow is null)
        {
            return [];
        }

        // Pincode.Code is globally unique (IPincodeRepository's own doc
        // comment), so this resolves unambiguously without needing the
        // city context. Null if the snapshot's pincode text no longer
        // matches any master row (stale/free-text drift) - candidates
        // whose area is pincode-scoped simply can't be verified and are
        // excluded below, same as a real mismatch.
        var pincode = await _context.Set<Pincode>().AsNoTracking()
            .FirstOrDefaultAsync(p => p.Code == booking.AddressPincodeSnapshot, cancellationToken);
        Guid? pincodeId = pincode?.Id;

        var excluded = excludeProviderIds ?? [];

        // ponytail: zone-level ProviderServiceArea scoping is not checked -
        // Booking has no LocalityId (only free-text city/pincode snapshots),
        // and Zone is only reachable through Locality, so there is no
        // reliable way to resolve which zone a booking's address falls in
        // from what Booking actually stores today. City + pincode (both
        // resolvable) are enforced; upgrade path is snapshotting
        // Booking.LocalityId at creation, the same way SourceAddressId
        // already is, if zone-scoped coverage turns out to matter in
        // practice.
        var providers = await _context.Set<Provider>()
            .AsNoTracking()
            .Where(p => p.Status == ProviderStatus.Active && !excluded.Contains(p.Id))
            .Where(p => _context.Set<ProviderSkillMapping>().Any(s =>
                s.ProviderId == p.Id && s.IsActive && s.CategoryId == service.CategoryId &&
                (s.ServiceId == null || s.ServiceId == serviceId)))
            .Where(p => _context.Set<ProviderServiceArea>().Any(a =>
                a.ProviderId == p.Id && a.IsActive && a.CityId == slotWindow.CityId &&
                (a.PincodeId == null || a.PincodeId == pincodeId)))
            .ToListAsync(cancellationToken);

        var byStraightLine = OrderByStraightLineDistance(booking, providers);

        return await RankByTravelTimeAsync(booking, byStraightLine, cancellationToken);
    }

    /// <summary>A candidate and its great-circle distance, before task 267 has had a chance to reorder anything.</summary>
    private sealed record StraightLineCandidate(Provider Provider, decimal? DistanceKm);

    /// <summary>
    /// Nearest first. A candidate either side couldn't be measured for (no
    /// coordinates on one or both ends) sorts after all of those, ordered only
    /// by Id for a deterministic result - not a ranking signal (PROVIDER.md
    /// OPEN DECISIONS - AUTOMATIC ASSIGNMENT #3: no rating input exists to
    /// break ties with instead).
    /// </summary>
    private static List<StraightLineCandidate> OrderByStraightLineDistance(Booking booking, List<Provider> providers) =>
        providers
            .Select(p => new StraightLineCandidate(
                p,
                GeoDistance.KilometresBetween(booking.AddressLatitudeSnapshot, booking.AddressLongitudeSnapshot, p.Latitude, p.Longitude)))
            .OrderBy(c => c.DistanceKm.HasValue ? 0 : 1)
            .ThenBy(c => c.DistanceKm)
            .ThenBy(c => c.Provider.Id)
            .ToList();

    /// <summary>
    /// Reorders the straight-line-nearest candidates by measured road travel
    /// time, leaving every other candidate exactly where straight-line
    /// distance put it. See this class's remarks for the direction
    /// approximation and the sandbox rule.
    /// </summary>
    private async Task<IReadOnlyList<ProviderMatchCandidate>> RankByTravelTimeAsync(
        Booking booking,
        List<StraightLineCandidate> byStraightLine,
        CancellationToken cancellationToken)
    {
        var options = _options.Value;
        if (!options.RouteRankingEnabled)
        {
            return WithoutTravelTime(byStraightLine);
        }

        // TakeWhile, not Where: the list is ascending by distance with the
        // unmeasurable candidates last, so the in-radius candidates are a
        // prefix of it. Taking a prefix is what lets the unpriced remainder
        // below simply be the rest of the list, in the order it is already in.
        var priced = byStraightLine
            .TakeWhile(c => c.DistanceKm <= options.RouteRankingRadiusKm)
            .Take(options.MaxRouteCandidates)
            .ToList();

        if (priced.Count == 0)
        {
            // Everyone is beyond the radius, or nobody has coordinates.
            // Straight-line ordering already ranks them; a route call would
            // only cost money to confirm what the ordering says.
            return WithoutTravelTime(byStraightLine);
        }

        // The booking's coordinate snapshot is non-nullable, so an origin
        // always exists; only the provider end can be missing, and TakeWhile
        // has already dropped those.
        var origin = new GeoCoordinate(booking.AddressLatitudeSnapshot, booking.AddressLongitudeSnapshot);
        var destinations = priced
            .Select(c => new GeoCoordinate(c.Provider.Latitude!.Value, c.Provider.Longitude!.Value))
            .ToList();

        var estimates = await _routeEstimateProvider.EstimateAsync(origin, destinations, cancellationToken);
        int[]? durationSeconds = RoadDurationsByDestination(estimates, destinations.Count);
        if (durationSeconds is null)
        {
            return WithoutTravelTime(byStraightLine);
        }

        var byTravelTime = priced
            .Select((candidate, destinationIndex) => new ProviderMatchCandidate(
                candidate.Provider.Id, candidate.DistanceKm, durationSeconds[destinationIndex]))
            .OrderBy(c => c.TravelDurationSeconds)
            .ThenBy(c => c.ProviderId)
            .ToList();

        return [.. byTravelTime, .. WithoutTravelTime(byStraightLine.Skip(priced.Count))];
    }

    /// <summary>
    /// Travel times indexed by destination position, or <c>null</c> when the
    /// response carries no road information worth reordering by - every
    /// estimate degraded to the sandbox (see this class's remarks), or the
    /// one-estimate-per-destination contract was not met. The latter cannot
    /// happen through either shipped implementation; it is checked because the
    /// alternative to checking is ranking a candidate first on a duration that
    /// was never measured.
    /// </summary>
    private static int[]? RoadDurationsByDestination(IReadOnlyList<RouteEstimate> estimates, int destinationCount)
    {
        if (estimates.Count != destinationCount || estimates.All(e => e.Source == RouteEstimateSource.Sandbox))
        {
            return null;
        }

        var durationSeconds = new int?[destinationCount];
        foreach (var estimate in estimates)
        {
            if (estimate.DestinationIndex < 0 || estimate.DestinationIndex >= destinationCount)
            {
                return null;
            }

            durationSeconds[estimate.DestinationIndex] = estimate.DurationSeconds;
        }

        return durationSeconds.All(seconds => seconds.HasValue)
            ? [.. durationSeconds.Select(seconds => seconds!.Value)]
            : null;
    }

    private static List<ProviderMatchCandidate> WithoutTravelTime(IEnumerable<StraightLineCandidate> candidates) =>
        candidates.Select(c => new ProviderMatchCandidate(c.Provider.Id, c.DistanceKm)).ToList();
}

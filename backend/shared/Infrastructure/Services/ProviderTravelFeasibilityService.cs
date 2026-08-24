using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nestly.Application.ProviderManagement;
using Nestly.Application.Routing;
using Nestly.Domain;
using Nestly.Infrastructure.Options;
using Nestly.Infrastructure.Persistence;

namespace Nestly.Infrastructure.Services;

/// <summary>See <see cref="IProviderTravelFeasibilityService"/>.</summary>
/// <remarks>
/// <para>
/// <b>The direction approximation.</b> The leg before this booking is really
/// driven previous-job -> booking, but the batched routing seam takes one
/// origin and many destinations, so both neighbours are asked about from a
/// single origin - this booking's address - and the answer for the previous
/// job is treated as that inbound leg. It is the same mirror-question trade
/// <see cref="ProviderMatchingService"/> documents, for the same reason: one
/// request per candidate instead of two, at the same billed element count.
/// Traffic is directional, so the two numbers differ; a tidal-flow artery is
/// where they differ most. The following leg is asked in its true direction
/// and needs no approximation.
/// </para>
/// <para>
/// <b>The lookup budget is per instance, and the instance is scoped.</b> One
/// eligibility pass walks many candidates through
/// <see cref="ProviderAssignmentEligibilityService"/>, which holds one of these
/// for the life of its own DI scope - so counting on an instance field bounds
/// the whole pass, without threading a budget object through an interface whose
/// callers have no business knowing routing exists. A pass never outlives its
/// scope, so a per-scope budget is never looser than the per-pass cap it is
/// there to enforce. The counter is deliberately never reset: a scope that has
/// already spent its budget is exactly the one that should stop spending.
/// </para>
/// </remarks>
public class ProviderTravelFeasibilityService : IProviderTravelFeasibilityService
{
    private const int SecondsPerMinute = 60;

    // Same cross-aggregate join as ProviderScheduleConflictService: who is
    // committed lives on BookingProviderAssignment, when and where the job is
    // lives on Booking, and no single repository owns that join.
    private readonly NestlyDbContext _context;
    private readonly IRouteEstimateProvider _routeEstimateProvider;

    // The sandbox estimator by concrete type, exactly as
    // GoogleMapsRouteEstimateProvider takes it: it is this check's floor when
    // the budget is spent or a response cannot be used, and it is registered
    // unconditionally whichever implementation IRouteEstimateProvider binds to.
    private readonly SandboxRouteEstimateProvider _sandboxRouteEstimateProvider;
    private readonly IOptions<AutoAssignmentOptions> _options;
    private readonly ILogger<ProviderTravelFeasibilityService> _logger;
    private readonly IProviderJobOccupancyService _occupancyService;

    private int _routeLookupsSpent;
    private bool _budgetExhaustionLogged;

    public ProviderTravelFeasibilityService(
        NestlyDbContext context,
        IRouteEstimateProvider routeEstimateProvider,
        SandboxRouteEstimateProvider sandboxRouteEstimateProvider,
        IOptions<AutoAssignmentOptions> options,
        ILogger<ProviderTravelFeasibilityService> logger,
        IProviderJobOccupancyService occupancyService)
    {
        _context = context;
        _routeEstimateProvider = routeEstimateProvider;
        _sandboxRouteEstimateProvider = sandboxRouteEstimateProvider;
        _options = options;
        _occupancyService = occupancyService;
        _logger = logger;
    }

    public async Task<ProviderTravelConflict?> FindConflictAsync(
        Guid providerId,
        Booking booking,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(booking);

        var options = _options.Value;
        if (!options.TravelBufferEnabled)
        {
            return null;
        }

        var neighbours = await FindAdjacentJobsAsync(providerId, booking, cancellationToken);
        if (neighbours.Count == 0)
        {
            // The overwhelmingly common case, and it costs nothing: a
            // candidate with an empty day has no leg to drive.
            return null;
        }

        var origin = new GeoCoordinate(booking.AddressLatitudeSnapshot, booking.AddressLongitudeSnapshot);
        var destinations = neighbours.Select(neighbour => neighbour.Address).ToList();
        var estimates = await EstimateLegsAsync(origin, destinations, cancellationToken);

        int bufferSeconds = options.TravelHandoverBufferMinutes * SecondsPerMinute;

        // Previous first, then following - the order FindAdjacentJobsAsync
        // built them in, so an admin reading the log for a candidate that was
        // impossible in both directions is told about the earlier one.
        for (int index = 0; index < neighbours.Count; index++)
        {
            var neighbour = neighbours[index];
            var estimate = estimates[index];

            // No drive, no buffer: see IProviderTravelFeasibilityService.
            int requiredSeconds = estimate.DurationSeconds == 0
                ? 0
                : estimate.DurationSeconds + bufferSeconds;

            if (neighbour.GapSeconds >= requiredSeconds)
            {
                continue;
            }

            var conflict = new ProviderTravelConflict(
                neighbour.BookingId, neighbour.Direction, neighbour.GapSeconds,
                estimate.DurationSeconds, requiredSeconds - estimate.DurationSeconds, estimate.Source);

            _logger.LogDebug(
                "Provider {ProviderId} cannot take booking {BookingId}: the {Direction} leg against booking {NeighbourBookingId} needs {RequiredSeconds}s ({TravelSeconds}s travel from {Source} plus {BufferSeconds}s buffer) but only {GapSeconds}s are free.",
                providerId, booking.Id, conflict.Direction, conflict.BookingId,
                requiredSeconds, conflict.TravelSeconds, conflict.Source, conflict.BufferSeconds, conflict.GapSeconds);

            return conflict;
        }

        return null;
    }

    /// <summary>One end of a drive: the adjacent job, and how much idle time separates it from this booking.</summary>
    private sealed record AdjacentJob(Guid BookingId, ProviderTravelDirection Direction, int GapSeconds, GeoCoordinate Address);

    /// <summary>
    /// The provider's last live job ending at or before this slot starts and
    /// their first one starting at or after it ends, on the same date - at most
    /// two rows, previous first.
    /// </summary>
    private async Task<List<AdjacentJob>> FindAdjacentJobsAsync(Guid providerId, Booking booking, CancellationToken cancellationToken)
    {
        // Statuses that still put the provider at a place that day - the same
        // set ProviderScheduleConflictService occupies on. Completed is included
        // so a just-finished job still counts as a leg to drive from, exactly as
        // it did while it stayed Accepted; using the actual finish time rather
        // than the slot end as the "free from" instant is the early-release
        // follow-up. A Rejected/Reassigned/Withdrawn row is nowhere anybody drives to.
        var liveStatuses = new[]
        {
            BookingProviderAssignmentStatus.Assigned,
            BookingProviderAssignmentStatus.Accepted,
            BookingProviderAssignmentStatus.Completed
        };

        // Narrowed in SQL to this provider's live jobs on this one date, then
        // ordered and compared in memory: TimeSpan comparisons don't reliably
        // translate on the SQLite provider this test suite runs against (see
        // ProviderScheduleConflictService and ProviderAvailabilityWindowRepository
        // for the same divergence), and a provider's day is a handful of rows.
        var sameDayJobs = await _context.Set<BookingProviderAssignment>()
            .Join(_context.Set<Booking>(), a => a.BookingId, b => b.Id, (a, b) => new { Assignment = a, Booking = b })
            .Where(x =>
                x.Assignment.ProviderId == providerId &&
                liveStatuses.Contains(x.Assignment.Status) &&
                x.Booking.SlotDate == booking.SlotDate &&
                x.Booking.Id != booking.Id)
            .Select(x => new
            {
                x.Booking.Id,
                StartTime = x.Booking.SlotStartTimeSnapshot,
                EndTime = x.Booking.SlotEndTimeSnapshot,
                x.Assignment.Status,
                x.Assignment.CompletedAt,
                x.Booking.IsDurationBasedSnapshot,
                Latitude = x.Booking.AddressLatitudeSnapshot,
                Longitude = x.Booking.AddressLongitudeSnapshot,
            })
            .ToListAsync(cancellationToken);

        var adjacent = new List<AdjacentJob>(2);

        // Effective end (IProviderJobOccupancyService): a verified-complete,
        // non-duration-based job's actual finish time rather than its slot's
        // own end - the early-release/overrun step. Only the previous leg
        // needs it: the following job's own start time is fixed and unaffected
        // by anyone else's completion.
        //
        // Ties broken by Id so a provider with two jobs ending at the same
        // instant produces the same answer on every pass. Any job that neither
        // ends before this slot nor starts after it overlaps it, and is task
        // 288's refusal rather than this one's.
        var previous = sameDayJobs
            .Select(job => new
            {
                job.Id,
                job.Latitude,
                job.Longitude,
                EffectiveEndTime = _occupancyService.EffectiveEndTime(new JobOccupancy(
                    job.Status, job.CompletedAt, job.IsDurationBasedSnapshot, booking.SlotDate, job.StartTime, job.EndTime)),
            })
            .Where(job => job.EffectiveEndTime <= booking.SlotStartTimeSnapshot)
            .OrderByDescending(job => job.EffectiveEndTime)
            .ThenBy(job => job.Id)
            .FirstOrDefault();
        if (previous is not null)
        {
            adjacent.Add(new AdjacentJob(
                previous.Id,
                ProviderTravelDirection.FromPreviousBooking,
                ToWholeSeconds(booking.SlotStartTimeSnapshot - previous.EffectiveEndTime),
                new GeoCoordinate(previous.Latitude, previous.Longitude)));
        }

        var following = sameDayJobs
            .Where(job => job.StartTime >= booking.SlotEndTimeSnapshot)
            .OrderBy(job => job.StartTime)
            .ThenBy(job => job.Id)
            .FirstOrDefault();
        if (following is not null)
        {
            adjacent.Add(new AdjacentJob(
                following.Id,
                ProviderTravelDirection.ToFollowingBooking,
                ToWholeSeconds(following.StartTime - booking.SlotEndTimeSnapshot),
                new GeoCoordinate(following.Latitude, following.Longitude)));
        }

        return adjacent;
    }

    /// <summary>
    /// One estimate per destination, in order, always - falling back to the
    /// local sandbox estimator rather than to "no opinion" whenever the maps
    /// provider cannot be asked (budget spent) or its answer cannot be used.
    /// Never returns fewer estimates than destinations, because the caller
    /// reads them by index and the alternative to an approximate leg is
    /// letting an impossible assignment through.
    /// </summary>
    private async Task<IReadOnlyList<RouteEstimate>> EstimateLegsAsync(
        GeoCoordinate origin,
        IReadOnlyList<GeoCoordinate> destinations,
        CancellationToken cancellationToken)
    {
        if (_routeLookupsSpent + destinations.Count > _options.Value.MaxTravelRouteLookups)
        {
            if (!_budgetExhaustionLogged)
            {
                _budgetExhaustionLogged = true;
                _logger.LogWarning(
                    "Travel-feasibility route lookups for this assignment pass are exhausted after {RouteLookupsSpent} of {MaxTravelRouteLookups}; remaining candidates are checked against the sandbox estimate instead.",
                    _routeLookupsSpent, _options.Value.MaxTravelRouteLookups);
            }

            return SandboxLegs(origin, destinations);
        }

        _routeLookupsSpent += destinations.Count;

        // One batched request for both neighbours - the seam is built for
        // exactly this, and two round trips per candidate would multiply the
        // latency on a path that must not hold up a booking.
        var estimates = await _routeEstimateProvider.EstimateAsync(origin, destinations, cancellationToken);

        return IsUsable(estimates, destinations.Count)
            ? estimates
            : Repaired(estimates, origin, destinations);
    }

    /// <summary>
    /// Whether the response honours the one-estimate-per-destination,
    /// index-aligned contract. Neither shipped implementation can break it; it
    /// is checked because the alternative to checking is an IndexOutOfRange or,
    /// worse, reading one leg's duration for another leg.
    /// </summary>
    private static bool IsUsable(IReadOnlyList<RouteEstimate> estimates, int destinationCount) =>
        estimates.Count == destinationCount &&
        estimates.Select((estimate, index) => estimate.DestinationIndex == index).All(aligned => aligned);

    /// <summary>
    /// Fills the gaps in an unusable response from the sandbox estimator,
    /// keeping whatever legs did come back correctly addressed. This is the
    /// "maps returned nothing" path, and it deliberately produces a number
    /// rather than a shrug.
    /// </summary>
    private IReadOnlyList<RouteEstimate> Repaired(
        IReadOnlyList<RouteEstimate> estimates,
        GeoCoordinate origin,
        IReadOnlyList<GeoCoordinate> destinations)
    {
        _logger.LogWarning(
            "Route estimates for the travel-feasibility check came back malformed ({EstimateCount} for {DestinationCount} destinations); falling back to the sandbox estimate for the legs that are missing.",
            estimates.Count, destinations.Count);

        var byIndex = new RouteEstimate?[destinations.Count];
        foreach (var estimate in estimates)
        {
            if (estimate.DestinationIndex >= 0 && estimate.DestinationIndex < destinations.Count)
            {
                byIndex[estimate.DestinationIndex] ??= estimate;
            }
        }

        return [.. byIndex.Select((estimate, index) => estimate ?? _sandboxRouteEstimateProvider.Estimate(origin, destinations[index], index))];
    }

    private IReadOnlyList<RouteEstimate> SandboxLegs(GeoCoordinate origin, IReadOnlyList<GeoCoordinate> destinations) =>
        [.. destinations.Select((destination, index) => _sandboxRouteEstimateProvider.Estimate(origin, destination, index))];

    /// <summary>
    /// Slot snapshots are whole minutes, so this never truncates anything real;
    /// it rounds down regardless, because a partial second of slack is not
    /// slack.
    /// </summary>
    private static int ToWholeSeconds(TimeSpan gap) => (int)gap.TotalSeconds;
}

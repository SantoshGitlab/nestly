using Nestly.BuildingBlocks.Geo;
using Nestly.BuildingBlocks.Primitives;
using Nestly.Domain.Events;

namespace Nestly.Domain;

/// <summary>
/// Which router produced a stored ETA (task 271), so a support agent looking
/// at "arriving in 6 minutes" can tell a real traffic-aware answer from an
/// approximation produced while the maps integration was degraded.
/// </summary>
/// <remarks>
/// A deliberate second enum rather than a reuse of
/// <c>Nestly.Application.Routing.RouteEstimateSource</c>: Domain must not
/// depend on Application (the same layering rule that kept the provenance off
/// <see cref="BookingEtaUpdatedEvent"/>), and a persisted vocabulary belongs to
/// the entity that persists it. Exactly one place maps between the two -
/// <c>BookingEtaService</c> - so the pair cannot drift silently: adding a
/// member to either without the other fails to compile there.
///
/// Members are appended, never inserted between existing ones - see
/// <see cref="BookingStatus"/>.
/// </remarks>
public enum BookingEtaSource
{
    /// <summary>Great-circle distance x a road-winding factor at a fixed average speed - an approximation.</summary>
    Sandbox,

    /// <summary>Google Maps road routing.</summary>
    GoogleMaps
}

/// <summary>
/// One row per booking holding the live tracking state a customer or an admin
/// reads while a job is in flight (task 271) - today, the arrival estimate.
/// </summary>
/// <remarks>
/// <para>
/// Separate from <see cref="Booking"/> on purpose. This row is rewritten every
/// time a fresh estimate lands, whereas a booking is a mostly-immutable
/// commercial record; hanging a high-churn column set off it would put the
/// tracking write path in contention with every other booking write, and would
/// mean an ETA recompute has to load and re-save an aggregate that carries
/// items, add-ons and status history.
/// </para>
/// <para>
/// Also separate from <see cref="ProviderLocationPing"/>: the ping trail is
/// append-only history of where someone was, this is a single current answer to
/// "when will they get here". The trail can hold hundreds of rows per job and
/// is pruned; this row is one row and is read on every tracking poll.
/// </para>
/// <para>
/// <b>Deliberately not stored here:</b> the provider's current coordinates
/// (that is <see cref="Provider"/>'s last-known pair plus the ping trail, and a
/// third copy would be a third chance to disagree), any ETA history (the event
/// stream carries changes; a per-recompute audit of an estimate has no reader),
/// and arrival/lateness flags (nothing computes them yet - tasks 275 and 284
/// can add what they actually need rather than inheriting guesses).
/// </para>
/// <para>
/// An <see cref="AggregateRoot{TId}"/> rather than a plain
/// <see cref="Entity{TId}"/> because it raises
/// <see cref="BookingEtaUpdatedEvent"/> and <c>DomainEventDispatchInterceptor</c>
/// only scans aggregate roots - the same reason
/// <see cref="ProviderLocationPing"/> is one.
/// </para>
/// </remarks>
public class BookingTracking : AggregateRoot<Guid>
{
    private const decimal MinimumLatitude = -90m;
    private const decimal MaximumLatitude = 90m;
    private const decimal MinimumLongitude = -180m;
    private const decimal MaximumLongitude = 180m;

    /// <summary>
    /// How much an ETA has to move before it counts as news.
    /// </summary>
    /// <remarks>
    /// Sixty seconds, because every surface that renders this number renders it
    /// rounded to whole minutes ("arriving in 8 minutes") - a change smaller
    /// than a minute frequently cannot change a single pixel the customer sees,
    /// so raising an event for it would push a broadcast (task 274) and
    /// potentially a notification for nothing. It is also comfortably above the
    /// run-to-run noise a routing provider returns for an unchanged route,
    /// which is the jitter this threshold exists to absorb: a provider stopped
    /// at a light produces a stream of estimates wobbling by a few seconds, and
    /// none of them is an event.
    ///
    /// Absolute rather than proportional. A proportional threshold would be
    /// silent exactly where the customer cares most - at two minutes out, 10%
    /// is 12 seconds of slack on a number they are watching the door for -
    /// while being far too twitchy an hour away. If a proportional rule is ever
    /// wanted it belongs here, not spread across callers.
    /// </remarks>
    public const int MaterialEtaChangeSeconds = 60;

    public Guid BookingId { get; private set; }

    /// <summary>
    /// The provider the current estimate was computed for, or null when there
    /// is no estimate. Nullable for the same reason
    /// <see cref="Booking.AssignedProviderId"/> is: a tracking row can exist
    /// before or after anyone is on the job.
    /// </summary>
    public Guid? ProviderId { get; private set; }

    /// <summary>
    /// Estimated remaining road-travel time to the booking's address, in
    /// seconds, or null when there is no usable estimate - which includes
    /// "the booking is no longer trackable", see <see cref="ClearEta"/>.
    /// </summary>
    public int? EtaSeconds { get; private set; }

    /// <summary>Remaining road distance in metres - the same estimate's other half, free in the same response and what a live-ops list renders next to the time.</summary>
    public int? EtaDistanceMetres { get; private set; }

    /// <summary>When the estimate was produced. An ETA without this is unreadable a minute later.</summary>
    public DateTime? EtaComputedAtUtc { get; private set; }

    public BookingEtaSource? EtaSource { get; private set; }

    /// <summary>
    /// The provider fix the current estimate was computed from. Stored because
    /// it is the baseline the movement half of the recompute throttle measures
    /// against (<see cref="ShouldRecompute"/>) - "has the provider moved far
    /// enough since the ETA to make it wrong" cannot be answered from the
    /// latest ping alone.
    /// </summary>
    public decimal? EtaOriginLatitude { get; private set; }

    public decimal? EtaOriginLongitude { get; private set; }

    protected BookingTracking() { }

    public BookingTracking(Guid id, Guid bookingId)
        : base(id)
    {
        if (bookingId == Guid.Empty)
        {
            throw new ArgumentException("Booking is required.", nameof(bookingId));
        }

        BookingId = bookingId;
    }

    /// <summary>Whether a usable arrival estimate is currently stored.</summary>
    public bool HasEta => EtaSeconds is not null;

    /// <summary>
    /// Whether a provider fix at <paramref name="latitude"/>/<paramref name="longitude"/>
    /// at <paramref name="nowUtc"/> justifies paying for a fresh route lookup.
    /// </summary>
    /// <remarks>
    /// The two gates are independent and either one opens: enough time has
    /// passed, or the provider has moved far enough that the stored answer is
    /// stale regardless of how recently it was computed. That is the point of
    /// the feature - ETA cost is decoupled from ping frequency, so a client
    /// pinging every second and a client pinging every fifteen cost the same,
    /// while a provider covering ground fast still gets a current number.
    ///
    /// The thresholds are parameters rather than constants because they are
    /// operational cost knobs, not domain rules; <c>BookingEtaOptions</c> owns
    /// their values and their defaults.
    /// </remarks>
    public bool ShouldRecompute(
        DateTime nowUtc,
        decimal latitude,
        decimal longitude,
        TimeSpan minimumInterval,
        decimal minimumMovementMetres)
    {
        // No estimate yet, or one computed from an unknown position: there is
        // nothing to throttle against, and the first ETA of a job is the one
        // the customer is waiting for.
        if (EtaComputedAtUtc is null || EtaOriginLatitude is null || EtaOriginLongitude is null)
        {
            return true;
        }

        if (nowUtc - EtaComputedAtUtc.Value >= minimumInterval)
        {
            return true;
        }

        decimal? movedMetres = GeoDistance.MetresBetween(
            EtaOriginLatitude, EtaOriginLongitude, latitude, longitude);

        return movedMetres > minimumMovementMetres;
    }

    /// <summary>
    /// Stores a freshly computed estimate, raising
    /// <see cref="BookingEtaUpdatedEvent"/> only when the new number is
    /// materially different from the one it replaces
    /// (<see cref="MaterialEtaChangeSeconds"/>).
    /// </summary>
    /// <remarks>
    /// The row is always updated, even when the change is immaterial: the
    /// stored value is the freshest known answer, and
    /// <see cref="EtaComputedAtUtc"/>/<see cref="EtaOriginLatitude"/> are the
    /// throttle's own baseline, so leaving them behind would make the next
    /// recompute decision fire off a position and a time the system has already
    /// moved past. Only the *announcement* is suppressed.
    /// </remarks>
    public void ApplyEta(
        Guid? providerId,
        int etaSeconds,
        int distanceMetres,
        BookingEtaSource source,
        decimal originLatitude,
        decimal originLongitude,
        DateTime computedAtUtc)
    {
        if (etaSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(etaSeconds), "An ETA cannot be negative.");
        }

        if (distanceMetres < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(distanceMetres), "A distance cannot be negative.");
        }

        if (originLatitude is < MinimumLatitude or > MaximumLatitude)
        {
            throw new ArgumentOutOfRangeException(nameof(originLatitude), "Latitude must be between -90 and 90.");
        }

        if (originLongitude is < MinimumLongitude or > MaximumLongitude)
        {
            throw new ArgumentOutOfRangeException(nameof(originLongitude), "Longitude must be between -180 and 180.");
        }

        bool isMaterialChange = EtaSeconds is null ||
            Math.Abs(etaSeconds - EtaSeconds.Value) >= MaterialEtaChangeSeconds;

        ProviderId = providerId;
        EtaSeconds = etaSeconds;
        EtaDistanceMetres = distanceMetres;
        EtaSource = source;
        EtaOriginLatitude = originLatitude;
        EtaOriginLongitude = originLongitude;
        EtaComputedAtUtc = computedAtUtc;

        if (isMaterialChange)
        {
            RaiseDomainEvent(new BookingEtaUpdatedEvent(BookingId, providerId, etaSeconds, computedAtUtc));
        }
    }

    /// <summary>
    /// Drops the stored estimate, leaving the row with no ETA at all.
    /// </summary>
    /// <returns><c>true</c> when something was actually cleared, so a caller can skip a pointless write.</returns>
    /// <remarks>
    /// A stale "arriving in 4 minutes" on a booking that has been completed or
    /// cancelled is worse than no ETA - it is a claim about the future that the
    /// platform knows to be false. The whole estimate goes, including its
    /// origin and provenance, so nothing downstream can reconstruct a
    /// half-answer from the leftovers.
    ///
    /// No event is raised: <see cref="BookingEtaUpdatedEvent"/> carries a
    /// non-nullable <c>EtaSeconds</c> and cannot express "there is no longer an
    /// ETA". The status transition that caused the clear is itself already on
    /// the event stream as <see cref="BookingStatusChangedEvent"/>, which is
    /// what a tracking screen reacts to when a job ends.
    /// </remarks>
    public bool ClearEta()
    {
        if (EtaSeconds is null && EtaComputedAtUtc is null && EtaSource is null &&
            EtaDistanceMetres is null && EtaOriginLatitude is null && EtaOriginLongitude is null)
        {
            return false;
        }

        EtaSeconds = null;
        EtaDistanceMetres = null;
        EtaComputedAtUtc = null;
        EtaSource = null;
        EtaOriginLatitude = null;
        EtaOriginLongitude = null;
        return true;
    }
}

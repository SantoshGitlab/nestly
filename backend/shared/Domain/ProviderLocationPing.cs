using Nestly.BuildingBlocks.Primitives;
using Nestly.Domain.Events;

namespace Nestly.Domain;

/// <summary>
/// How a <see cref="ProviderLocationPing"/> was produced (task 268). New
/// members are appended, never inserted between existing ones: no API
/// registers a JsonStringEnumConverter, so an enum that reaches a client
/// crosses the wire as its ordinal (see <see cref="BookingStatus"/>).
/// </summary>
public enum ProviderLocationSource
{
    /// <summary>Reported by the provider's own app from the device's geolocation API - the only real source in v1.</summary>
    ProviderApp,

    /// <summary>
    /// Not a real fix: generated rather than observed (the sandbox route
    /// provider, demos, load tests). Never treat a simulated ping as evidence
    /// of where a provider actually was.
    /// </summary>
    Simulated
}

/// <summary>
/// One position fix on a provider's location trail (task 268). Append-only by
/// construction: there are no mutators, and nothing rewrites a fix once it has
/// been stored - a trail that can be edited after the fact is not a trail.
/// </summary>
/// <remarks>
/// <see cref="Provider.Latitude"/>/<see cref="Provider.Longitude"/> stay the
/// single last-known pair the assignment engine ranks on; this type is the
/// history behind that pair, which is what an ETA, a live "where is my
/// technician" map and a post-hoc dispute review each need and a single
/// mutable point cannot answer.
/// <para>
/// RETENTION: this is operational data, not history to keep forever. A trail
/// exists to drive a live tracking screen and to settle a dispute shortly
/// afterwards; once a booking is closed and past that dispute window its pings
/// are pruned, and pings never attached to a booking (<see cref="BookingId"/>
/// null) are pruned sooner still. Nothing outside live tracking and
/// short-window dispute review may be built on a ping still existing - it is
/// not an audit log (<see cref="AuditLog"/> is), and a permanent
/// minute-by-minute movement history of a worker is a privacy liability with
/// no operational payoff. The pruning job that enforces this is not built here
/// (docs/TRACKING.md, task 287).
/// </para>
/// <para>
/// An <see cref="AggregateRoot{TId}"/> rather than a plain
/// <see cref="Entity{TId}"/> since task 272, for the same reason as
/// <see cref="BookingProviderAssignment"/>: it is already its own root (own
/// table, own repository, no owning navigation), and
/// <c>DomainEventDispatchInterceptor</c> only scans aggregate roots, so
/// <see cref="ProviderLocationUpdatedEvent"/> would otherwise be raised and
/// silently dropped.
/// </para>
/// </remarks>
public class ProviderLocationPing : AggregateRoot<Guid>
{
    private const decimal MinimumLatitude = -90m;
    private const decimal MaximumLatitude = 90m;
    private const decimal MinimumLongitude = -180m;
    private const decimal MaximumLongitude = 180m;

    public Guid ProviderId { get; private set; }

    /// <summary>
    /// The job this fix belongs to, or null when the provider was sharing a
    /// location outside any job (idle/on-shift). Nullable rather than a second
    /// table: the shape is identical and only the trail read cares.
    /// </summary>
    public Guid? BookingId { get; private set; }

    /// <summary>Same decimal(9,6) shape as <see cref="Provider.Latitude"/>/<see cref="Provider.Longitude"/> - see ProviderConfiguration.</summary>
    public decimal Latitude { get; private set; }

    public decimal Longitude { get; private set; }

    /// <summary>
    /// Device-reported horizontal accuracy in metres; null when the device did
    /// not report one. Stored rather than used to reject: a 300 m fix in a
    /// basement is still better than no fix, and how much to trust it is the
    /// reader's decision.
    /// </summary>
    public decimal? AccuracyMetres { get; private set; }

    /// <summary>When the device took the fix, by the device's own clock.</summary>
    public DateTime RecordedAtUtc { get; private set; }

    /// <summary>
    /// When the server stored the fix. Both timestamps are kept because they
    /// diverge for real reasons - a queued upload over a bad connection, or a
    /// device whose clock is simply wrong - and a reader deciding whether a
    /// fix is stale needs to see the gap rather than have it hidden. The
    /// entity therefore does not require one to precede the other; deciding
    /// how much skew to accept belongs to the ingest endpoint (task 269).
    /// </summary>
    public DateTime ReceivedAtUtc { get; private set; }

    public ProviderLocationSource Source { get; private set; }

    protected ProviderLocationPing() { }

    public ProviderLocationPing(
        Guid id,
        Guid providerId,
        Guid? bookingId,
        decimal latitude,
        decimal longitude,
        decimal? accuracyMetres,
        DateTime recordedAtUtc,
        DateTime receivedAtUtc,
        ProviderLocationSource source = ProviderLocationSource.ProviderApp)
        : base(id)
    {
        if (providerId == Guid.Empty)
        {
            throw new ArgumentException("Provider is required.", nameof(providerId));
        }

        if (latitude is < MinimumLatitude or > MaximumLatitude)
        {
            throw new ArgumentOutOfRangeException(nameof(latitude), "Latitude must be between -90 and 90.");
        }

        if (longitude is < MinimumLongitude or > MaximumLongitude)
        {
            throw new ArgumentOutOfRangeException(nameof(longitude), "Longitude must be between -180 and 180.");
        }

        if (accuracyMetres is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(accuracyMetres), "Accuracy cannot be negative.");
        }

        ProviderId = providerId;
        BookingId = bookingId;
        Latitude = latitude;
        Longitude = longitude;
        AccuracyMetres = accuracyMetres;
        RecordedAtUtc = recordedAtUtc;
        ReceivedAtUtc = receivedAtUtc;
        Source = source;

        // Raised on construction rather than by the ingest endpoint (task
        // 269): appending a fix IS the event, this type has no mutators for a
        // later raise to hang off, and EF materializes through the protected
        // parameterless constructor, so reading a trail back never re-raises.
        RaiseDomainEvent(new ProviderLocationUpdatedEvent(
            Id, ProviderId, BookingId, Latitude, Longitude, AccuracyMetres, RecordedAtUtc));
    }
}

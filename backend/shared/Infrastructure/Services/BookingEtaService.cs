using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nestly.Application;
using Nestly.Application.Bookings;
using Nestly.Application.Routing;
using Nestly.Application.Tracking;
using Nestly.Domain;
using Nestly.Infrastructure.Options;

namespace Nestly.Infrastructure.Services;

/// <inheritdoc cref="IBookingEtaService"/>
public class BookingEtaService : IBookingEtaService
{
    private readonly IBookingRepository _bookingRepository;
    private readonly IBookingTrackingRepository _trackingRepository;
    private readonly IProviderLocationPingRepository _pingRepository;
    private readonly IRouteEstimateProvider _routeEstimateProvider;

    // The sandbox estimator by concrete type, exactly as
    // ProviderTravelFeasibilityService takes it: it is the floor when a
    // response cannot be used, and it is registered unconditionally whichever
    // implementation IRouteEstimateProvider binds to.
    private readonly SandboxRouteEstimateProvider _sandboxRouteEstimateProvider;
    private readonly BookingEtaOptions _options;
    private readonly ILogger<BookingEtaService> _logger;

    public BookingEtaService(
        IBookingRepository bookingRepository,
        IBookingTrackingRepository trackingRepository,
        IProviderLocationPingRepository pingRepository,
        IRouteEstimateProvider routeEstimateProvider,
        SandboxRouteEstimateProvider sandboxRouteEstimateProvider,
        IOptions<BookingEtaOptions> options,
        ILogger<BookingEtaService> logger)
    {
        _bookingRepository = bookingRepository;
        _trackingRepository = trackingRepository;
        _pingRepository = pingRepository;
        _routeEstimateProvider = routeEstimateProvider;
        _sandboxRouteEstimateProvider = sandboxRouteEstimateProvider;
        _options = options.Value;
        _logger = logger;
    }

    public Task RefreshAsync(Guid bookingId, CancellationToken cancellationToken = default) =>
        RunBestEffortAsync(() => RefreshCoreAsync(bookingId, cancellationToken), bookingId, cancellationToken);

    public Task ClearAsync(Guid bookingId, CancellationToken cancellationToken = default) =>
        RunBestEffortAsync(() => ClearCoreAsync(bookingId), bookingId, cancellationToken);

    private async Task RefreshCoreAsync(Guid bookingId, CancellationToken cancellationToken)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId);
        if (booking is null)
        {
            return;
        }

        // BookingLifecycle.IsTrackable, not a local copy of the same four
        // statuses - task 273 put that set in the domain precisely so the
        // tracking hub, the ingest endpoint and this cannot drift into
        // different answers. Reached here on a job that has finished (a ping
        // accepted a moment before completion, say), the honest answer is to
        // take the estimate away rather than leave the last one standing.
        if (!BookingLifecycle.IsTrackable(booking.Status))
        {
            await ClearCoreAsync(bookingId);
            return;
        }

        var latestFix = await _pingRepository.GetLatestForBookingAsync(bookingId);
        if (latestFix is null)
        {
            // Nothing has been reported yet. An ETA computed from the
            // provider's last-known coordinate off some other job would be a
            // number about the wrong journey.
            return;
        }

        var tracking = await _trackingRepository.GetByBookingAsync(bookingId);
        bool isNewRow = tracking is null;
        tracking ??= new BookingTracking(Guid.NewGuid(), bookingId);

        var nowUtc = DateTime.UtcNow;
        if (!tracking.ShouldRecompute(
                nowUtc,
                latestFix.Latitude,
                latestFix.Longitude,
                TimeSpan.FromSeconds(_options.MinimumRecomputeIntervalSeconds),
                _options.MinimumMovementMetres))
        {
            return;
        }

        var origin = new GeoCoordinate(latestFix.Latitude, latestFix.Longitude);
        var destination = new GeoCoordinate(booking.AddressLatitudeSnapshot, booking.AddressLongitudeSnapshot);
        var estimate = await EstimateLegAsync(origin, destination, cancellationToken);

        tracking.ApplyEta(
            booking.AssignedProviderId ?? latestFix.ProviderId,
            estimate.DurationSeconds,
            estimate.DistanceMetres,
            ToEtaSource(estimate.Source),
            latestFix.Latitude,
            latestFix.Longitude,
            nowUtc);

        if (isNewRow)
        {
            await _trackingRepository.AddAsync(tracking);
        }
        else
        {
            await _trackingRepository.UpdateAsync(tracking);
        }
    }

    private async Task ClearCoreAsync(Guid bookingId)
    {
        var tracking = await _trackingRepository.GetByBookingAsync(bookingId);

        // No row means no ETA, which is already the desired state - creating
        // one to record the absence of an estimate would put a row on every
        // booking that ever reached a terminal status.
        if (tracking is null || !tracking.ClearEta())
        {
            return;
        }

        await _trackingRepository.UpdateAsync(tracking);
    }

    /// <summary>
    /// One estimate for the single leg, always. <see cref="IRouteEstimateProvider"/>
    /// promises exactly that and neither shipped implementation breaks the
    /// promise; it is checked anyway because the alternative to checking is an
    /// index-out-of-range on the tracking write path, and because the
    /// documented degradation of the maps integration is precisely "return
    /// something approximate rather than nothing".
    /// </summary>
    private async Task<RouteEstimate> EstimateLegAsync(
        GeoCoordinate origin,
        GeoCoordinate destination,
        CancellationToken cancellationToken)
    {
        var estimates = await _routeEstimateProvider.EstimateAsync(origin, [destination], cancellationToken);

        var estimate = estimates.Count == 1 ? estimates[0] : null;
        if (estimate is not null)
        {
            return estimate;
        }

        _logger.LogWarning(
            "Route estimates for a booking ETA came back malformed ({EstimateCount} for 1 destination); falling back to the sandbox estimate.",
            estimates.Count);

        return _sandboxRouteEstimateProvider.Estimate(origin, destination, 0);
    }

    /// <summary>
    /// The single crossing between the routing seam's vocabulary and the
    /// persisted one. It exists so that Domain does not have to reference
    /// Application to describe where its own ETA came from, and it is
    /// exhaustive on purpose: a new <see cref="RouteEstimateSource"/> member
    /// stops compiling here rather than being silently filed as sandbox.
    /// </summary>
    private static BookingEtaSource ToEtaSource(RouteEstimateSource source) => source switch
    {
        RouteEstimateSource.GoogleMaps => BookingEtaSource.GoogleMaps,
        RouteEstimateSource.Sandbox => BookingEtaSource.Sandbox,
        _ => throw new ArgumentOutOfRangeException(nameof(source), source, "Unknown route estimate source.")
    };

    /// <summary>
    /// Runs the work and swallows anything that goes wrong with it - see
    /// <see cref="IBookingEtaService"/> for why an ETA must never be able to
    /// fail the caller that asked for it. Logged rather than ignored: a
    /// tracking screen quietly stuck on an old estimate is exactly the kind of
    /// fault nobody reports, so it has to be visible in the logs.
    /// </summary>
    /// <remarks>
    /// Cancellation is deliberately let through. That is the caller's own
    /// request going away during shutdown, not an ETA failure, and turning it
    /// into a logged warning would hide a healthy signal in noise.
    /// </remarks>
    private async Task RunBestEffortAsync(Func<Task> work, Guid bookingId, CancellationToken cancellationToken)
    {
        try
        {
            await work();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Could not update the arrival estimate for booking {BookingId}; the stored ETA is left as it was.",
                bookingId);
        }
    }
}

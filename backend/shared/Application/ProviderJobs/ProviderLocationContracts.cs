namespace Nestly.Application.ProviderJobs;

/// <summary>
/// One position fix pushed by the provider's own app while a job is in
/// flight (task 269, <c>POST /api/v1/jobs/{bookingId}/location</c>).
/// </summary>
/// <param name="Latitude">Degrees, -90..90.</param>
/// <param name="Longitude">Degrees, -180..180.</param>
/// <param name="AccuracyMetres">The device's own confidence radius; optional, never negative.</param>
/// <param name="RecordedAtUtc">
/// When the <i>device</i> took the fix, not when it managed to upload it.
/// A fix queued for minutes over a bad connection must not be shown as the
/// provider's current position, which is why this is a required field rather
/// than something the server infers from the arrival time.
/// </param>
public sealed record RecordProviderLocationRequest(
    decimal Latitude,
    decimal Longitude,
    decimal? AccuracyMetres,
    DateTime RecordedAtUtc);

/// <summary>
/// The outcome of one location push (task 269).
/// </summary>
/// <param name="Accepted">
/// False when the fix was dropped by the per-booking throttle. That is a
/// success, not an error - the client did nothing wrong, the platform simply
/// does not want data at that rate - so it is reported here rather than as a
/// failed <c>Result</c>, and the endpoint answers 202 instead of 200.
/// </param>
/// <param name="PingId">The stored ping, or null when the fix was dropped.</param>
/// <param name="NextAcceptedAfterUtc">
/// The device-clock time a subsequent fix must be at or after to be stored.
/// Lets a well-behaved client back off to exactly the accepted rate instead
/// of guessing.
/// </param>
public sealed record RecordProviderLocationResponse(
    bool Accepted,
    Guid? PingId,
    DateTime NextAcceptedAfterUtc);

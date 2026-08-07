using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.ProviderJobs;

/// <summary>
/// Ingest for the provider app's live position while a job is in flight
/// (task 269) - appends a <see cref="Nestly.Domain.ProviderLocationPing"/> and
/// refreshes the provider's denormalized last-known coordinate.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately its own service rather than another method on
/// <see cref="IProviderJobService"/>: that interface owns the job
/// <i>lifecycle</i> (offer, accept, start, complete) and its implementation
/// already carries four collaborators. Location ingest shares none of them
/// except the two it needs to answer "may this caller write here" - it is a
/// high-frequency write path with its own configuration, its own throttle and
/// its own privacy rules. Keeping it separate keeps both classes single-purpose
/// and lets this one be tested without the assignment lifecycle in the way.
/// </para>
/// <para>
/// Like every provider-facing service, <c>providerId</c> is the caller's own
/// id resolved from the JWT by the controller and never a route or body value
/// (SRS 28.3 IDOR).
/// </para>
/// </remarks>
public interface IProviderLocationIngestService
{
    /// <summary>
    /// Records one position fix against a booking, fail-closed.
    /// </summary>
    /// <returns>
    /// <list type="bullet">
    /// <item><see cref="ErrorType.Validation"/> - a coordinate, accuracy or timestamp outside the accepted bounds.</item>
    /// <item><see cref="ErrorType.NotFound"/> - no such booking.</item>
    /// <item><see cref="ErrorType.Forbidden"/> - the caller is not the provider on the booking's live assignment.</item>
    /// <item><see cref="ErrorType.Conflict"/> - the job is not in a state where location may be collected.</item>
    /// <item>Success with <see cref="RecordProviderLocationResponse.Accepted"/> false - dropped by the throttle.</item>
    /// </list>
    /// </returns>
    Task<Result<RecordProviderLocationResponse>> RecordAsync(
        Guid providerId,
        Guid bookingId,
        RecordProviderLocationRequest request);
}

namespace Nestly.Application.Tracking;

/// <summary>
/// Keeps a booking's stored arrival estimate current while the job is being
/// tracked (task 271), and removes it the moment the job stops being
/// trackable.
/// </summary>
/// <remarks>
/// <para>
/// <b>Best effort, by contract.</b> No method here reports failure and none of
/// them throws (except caller cancellation): an ETA is a convenience laid over
/// the location trail, and the trail, the ping that produced it and the
/// booking transition that triggered it are all already durable by the time
/// this is called. Failing an accepted location ping - which the provider's
/// app would then retry, re-pinging - because a routing lookup or a tracking
/// write went wrong would trade a real feature for a cosmetic one. Callers
/// therefore need no try/catch and have nothing to branch on.
/// </para>
/// <para>
/// <b>It decides for itself whether to spend anything.</b> Callers invoke it on
/// every accepted ping; the throttle that keeps route lookups from tracking
/// ping frequency lives inside, driven by <c>BookingEtaOptions</c>, so no
/// caller has to know that route lookups are billed.
/// </para>
/// </remarks>
public interface IBookingEtaService
{
    /// <summary>
    /// Recomputes and stores the booking's ETA if the throttle allows it, or
    /// clears any stored ETA if the booking is no longer trackable.
    /// </summary>
    Task RefreshAsync(Guid bookingId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unconditionally drops any stored ETA for the booking - the
    /// leaving-the-trackable-states path, where a stale "arriving in four
    /// minutes" on a completed or cancelled job is worse than no estimate at
    /// all.
    /// </summary>
    Task ClearAsync(Guid bookingId, CancellationToken cancellationToken = default);
}

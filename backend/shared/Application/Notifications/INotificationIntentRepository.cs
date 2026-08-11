using Nestly.Domain;

namespace Nestly.Application.Notifications;

/// <summary>
/// Persistence for the durable notification intents (task 294).
/// </summary>
/// <remarks>
/// Every state transition here is expressed as a <b>conditional UPDATE</b>
/// rather than load-mutate-save. That is not a performance choice: several app
/// instances run the same sweep against the same rows, and a read followed by
/// a write leaves a window in which two of them both decide a row is theirs.
/// The claim in particular is the entire concurrency story of this feature -
/// if it is ever rewritten as a tracked-entity update, the deduplication
/// guarantee goes with it.
/// </remarks>
public interface INotificationIntentRepository
{
    /// <summary>
    /// Takes the lease on one intent, atomically. Returns false when the row
    /// does not exist, is already terminal, is already leased by somebody
    /// else, or has exhausted <paramref name="maxAttempts"/> - all of which
    /// mean "not yours to send".
    /// </summary>
    Task<bool> TryClaimAsync(
        string dedupeKey,
        string leaseOwner,
        DateTime nowUtc,
        DateTime leaseExpiresAtUtc,
        int maxAttempts,
        CancellationToken cancellationToken = default);

    /// <summary>Marks a claimed intent delivered. Terminal.</summary>
    Task MarkDeliveredAsync(string dedupeKey, DateTime nowUtc, CancellationToken cancellationToken = default);

    /// <summary>Marks a pending intent as deliberately not sent. Terminal, and safe to call for a key that has no row (the fail-open path).</summary>
    Task MarkSkippedAsync(string dedupeKey, string reason, DateTime nowUtc, CancellationToken cancellationToken = default);

    /// <summary>Records why an attempt failed and releases the lease so the next sweep retries without waiting it out. Leaves the row pending; the attempt has already been counted by the claim.</summary>
    Task RecordFailureAsync(string dedupeKey, string error, CancellationToken cancellationToken = default);

    /// <summary>
    /// Intents that are still owed and are free to be worked on: pending, out
    /// of lease, under the retry bound, and older than
    /// <paramref name="createdBeforeUtc"/> so the in-process fast path is
    /// given its chance first.
    /// </summary>
    Task<IReadOnlyList<NotificationIntent>> ListSweepableAsync(
        DateTime nowUtc,
        DateTime createdBeforeUtc,
        int maxAttempts,
        int batchSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves every pending, unleased intent that has used up its attempts to
    /// <see cref="NotificationIntentStatus.Abandoned"/> and returns how many.
    /// The retry bound's terminal state - without it a permanently failing
    /// intent is selected by every sweep forever.
    /// </summary>
    Task<int> AbandonExhaustedAsync(
        DateTime nowUtc,
        int maxAttempts,
        string reason,
        CancellationToken cancellationToken = default);

    /// <summary>Diagnostics and tests; not on any delivery path.</summary>
    Task<NotificationIntent?> GetByDedupeKeyAsync(string dedupeKey, CancellationToken cancellationToken = default);
}

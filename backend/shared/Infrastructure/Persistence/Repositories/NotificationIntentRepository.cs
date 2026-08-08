using Microsoft.EntityFrameworkCore;
using Nestly.Application.Notifications;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

/// <summary>See <see cref="INotificationIntentRepository"/> - in particular why every mutation here is a conditional UPDATE.</summary>
public class NotificationIntentRepository : INotificationIntentRepository
{
    private readonly NestlyDbContext _context;

    public NotificationIntentRepository(NestlyDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// The whole concurrency story in one statement. <c>ExecuteUpdateAsync</c>
    /// compiles to a single <c>UPDATE ... WHERE</c>, so the predicate is
    /// evaluated by the database under the row lock it takes - two instances
    /// racing for the same intent produce one affected row and one zero.
    /// A tracked-entity read-modify-save here would let both through.
    /// </summary>
    public async Task<bool> TryClaimAsync(
        string dedupeKey,
        string leaseOwner,
        DateTime nowUtc,
        DateTime leaseExpiresAtUtc,
        int maxAttempts,
        CancellationToken cancellationToken = default)
    {
        var affected = await _context.NotificationIntents
            .Where(intent =>
                intent.DedupeKey == dedupeKey &&
                intent.Status == NotificationIntentStatus.Pending &&
                intent.AttemptCount < maxAttempts &&
                (intent.LeaseExpiresAtUtc == null || intent.LeaseExpiresAtUtc < nowUtc))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(intent => intent.AttemptCount, intent => intent.AttemptCount + 1)
                    .SetProperty(intent => intent.LastAttemptAtUtc, nowUtc)
                    .SetProperty(intent => intent.LeaseOwner, leaseOwner)
                    .SetProperty(intent => intent.LeaseExpiresAtUtc, leaseExpiresAtUtc),
                cancellationToken);

        return affected == 1;
    }

    public Task MarkDeliveredAsync(string dedupeKey, DateTime nowUtc, CancellationToken cancellationToken = default) =>
        _context.NotificationIntents
            .Where(intent => intent.DedupeKey == dedupeKey && intent.Status == NotificationIntentStatus.Pending)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(intent => intent.Status, NotificationIntentStatus.Delivered)
                    .SetProperty(intent => intent.CompletedAtUtc, nowUtc)
                    .SetProperty(intent => intent.LeaseOwner, (string?)null)
                    .SetProperty(intent => intent.LeaseExpiresAtUtc, (DateTime?)null),
                cancellationToken);

    public Task MarkSkippedAsync(string dedupeKey, string reason, DateTime nowUtc, CancellationToken cancellationToken = default) =>
        _context.NotificationIntents
            .Where(intent => intent.DedupeKey == dedupeKey && intent.Status == NotificationIntentStatus.Pending)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(intent => intent.Status, NotificationIntentStatus.Skipped)
                    .SetProperty(intent => intent.Resolution, Truncate(reason, 500))
                    .SetProperty(intent => intent.CompletedAtUtc, nowUtc)
                    .SetProperty(intent => intent.LeaseOwner, (string?)null)
                    .SetProperty(intent => intent.LeaseExpiresAtUtc, (DateTime?)null),
                cancellationToken);

    public Task RecordFailureAsync(string dedupeKey, string error, CancellationToken cancellationToken = default) =>
        _context.NotificationIntents
            .Where(intent => intent.DedupeKey == dedupeKey && intent.Status == NotificationIntentStatus.Pending)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(intent => intent.LastError, Truncate(error, 1000))
                    .SetProperty(intent => intent.LeaseOwner, (string?)null)
                    .SetProperty(intent => intent.LeaseExpiresAtUtc, (DateTime?)null),
                cancellationToken);

    /// <summary>
    /// Candidates only - selecting a row here confers no right to send it.
    /// The claim does that, and every consumer of this list must take one.
    /// </summary>
    public async Task<IReadOnlyList<NotificationIntent>> ListSweepableAsync(
        DateTime nowUtc,
        DateTime createdBeforeUtc,
        int maxAttempts,
        int batchSize,
        CancellationToken cancellationToken = default) =>
        await _context.NotificationIntents
            .AsNoTracking()
            .Where(intent =>
                intent.Status == NotificationIntentStatus.Pending &&
                intent.AttemptCount < maxAttempts &&
                intent.CreatedAtUtc <= createdBeforeUtc &&
                (intent.LeaseExpiresAtUtc == null || intent.LeaseExpiresAtUtc < nowUtc))
            .OrderBy(intent => intent.CreatedAtUtc)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Excludes leased rows: an intent on its final attempt is still in flight
    /// while somebody holds it, and abandoning it underneath a worker that is
    /// about to succeed would report a loss that did not happen.
    /// </summary>
    public Task<int> AbandonExhaustedAsync(
        DateTime nowUtc,
        int maxAttempts,
        string reason,
        CancellationToken cancellationToken = default) =>
        _context.NotificationIntents
            .Where(intent =>
                intent.Status == NotificationIntentStatus.Pending &&
                intent.AttemptCount >= maxAttempts &&
                (intent.LeaseExpiresAtUtc == null || intent.LeaseExpiresAtUtc < nowUtc))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(intent => intent.Status, NotificationIntentStatus.Abandoned)
                    .SetProperty(intent => intent.Resolution, Truncate(reason, 500))
                    .SetProperty(intent => intent.CompletedAtUtc, nowUtc)
                    .SetProperty(intent => intent.LeaseOwner, (string?)null)
                    .SetProperty(intent => intent.LeaseExpiresAtUtc, (DateTime?)null),
                cancellationToken);

    public Task<NotificationIntent?> GetByDedupeKeyAsync(string dedupeKey, CancellationToken cancellationToken = default) =>
        _context.NotificationIntents
            .AsNoTracking()
            .FirstOrDefaultAsync(intent => intent.DedupeKey == dedupeKey, cancellationToken);

    /// <summary>Keeps a long exception message from failing the very write that is meant to explain it.</summary>
    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}

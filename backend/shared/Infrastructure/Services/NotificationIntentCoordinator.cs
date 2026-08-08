using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nestly.Application.Notifications;
using Nestly.BuildingBlocks.Primitives;
using Nestly.Domain;
using Nestly.Infrastructure.Options;

namespace Nestly.Infrastructure.Services;

/// <summary>See <see cref="INotificationIntentCoordinator"/>.</summary>
public sealed class NotificationIntentCoordinator : INotificationIntentCoordinator
{
    /// <summary>
    /// Identifies this scope's claims in the <c>lease_owner</c> column. Machine
    /// name plus a per-scope id: enough for an operator reading the table to
    /// tell which box is stuck, and unique enough that two scopes on one box
    /// are never confused.
    /// </summary>
    private readonly string _leaseOwner = $"{Environment.MachineName}/{Guid.NewGuid():N}";

    /// <summary>
    /// Keys this scope already holds, so a sweep's claim and the handler's
    /// subsequent claim on the same row are recognised as the same worker
    /// rather than as a race - see <see cref="INotificationIntentCoordinator"/>.
    /// </summary>
    private readonly HashSet<string> _claimsHeld = new(StringComparer.Ordinal);

    private readonly INotificationIntentRepository _repository;
    private readonly IOptionsMonitor<NotificationIntentOptions> _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<NotificationIntentCoordinator> _logger;

    public NotificationIntentCoordinator(
        INotificationIntentRepository repository,
        IOptionsMonitor<NotificationIntentOptions> options,
        TimeProvider timeProvider,
        ILogger<NotificationIntentCoordinator> logger)
    {
        _repository = repository;
        _options = options;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task DeliverAsync(
        IDomainEvent domainEvent,
        NotificationEventType eventType,
        Func<CancellationToken, Task> send,
        CancellationToken cancellationToken = default)
    {
        var dedupeKey = NotificationIntent.BuildDedupeKey(domainEvent.EventId, eventType);
        var options = _options.CurrentValue;
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;

        var holdsClaim = _claimsHeld.Contains(dedupeKey);
        if (!holdsClaim)
        {
            var claimed = await _repository.TryClaimAsync(
                dedupeKey,
                _leaseOwner,
                nowUtc,
                nowUtc.AddSeconds(options.LeaseSeconds),
                options.MaxAttempts,
                cancellationToken);

            if (claimed)
            {
                _claimsHeld.Add(dedupeKey);
                holdsClaim = true;
            }
        }

        if (!holdsClaim)
        {
            // Either somebody else is sending it, it is already terminal, or
            // it has run out of attempts. The one remaining possibility - no
            // row at all - is the fail-open case, and it has to be
            // distinguished, because it means this message has no durability
            // behind it and somebody should know.
            var existing = await _repository.GetByDedupeKeyAsync(dedupeKey, cancellationToken);
            if (existing is not null)
            {
                _logger.LogDebug(
                    "Notification intent {DedupeKey} was not claimed (status {Status}, attempts {AttemptCount}); skipping this dispatch.",
                    dedupeKey, existing.Status, existing.AttemptCount);
                return;
            }

            _logger.LogWarning(
                "No durable intent exists for {EventType} from {DomainEventType} {DomainEventId}; dispatching without a retry path. " +
                "This notification is at-most-once - NotificationIntentPlanner does not plan for it, or the intent interceptor is not attached.",
                eventType, domainEvent.GetType().Name, domainEvent.EventId);

            await send(cancellationToken);
            return;
        }

        try
        {
            await send(cancellationToken);
        }
        catch (Exception exception)
        {
            _claimsHeld.Remove(dedupeKey);

            // Best effort by necessity: the send may well have failed because
            // the database is unreachable, in which case so will this. The
            // lease expiring is the backstop that does not need a write.
            try
            {
                await _repository.RecordFailureAsync(dedupeKey, exception.Message, cancellationToken);
            }
            catch (Exception bookkeepingFailure)
            {
                _logger.LogError(
                    bookkeepingFailure,
                    "Could not record the failure of notification intent {DedupeKey}; it will be retried once its lease expires.",
                    dedupeKey);
            }

            throw;
        }

        await _repository.MarkDeliveredAsync(dedupeKey, _timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
        _claimsHeld.Remove(dedupeKey);
    }

    public async Task SkipAsync(
        IDomainEvent domainEvent,
        NotificationEventType eventType,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var dedupeKey = NotificationIntent.BuildDedupeKey(domainEvent.EventId, eventType);

        await _repository.MarkSkippedAsync(dedupeKey, reason, _timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
        _claimsHeld.Remove(dedupeKey);
    }

    public async Task<bool> TryClaimForSweepAsync(NotificationIntent intent, CancellationToken cancellationToken = default)
    {
        var options = _options.CurrentValue;
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;

        var claimed = await _repository.TryClaimAsync(
            intent.DedupeKey,
            _leaseOwner,
            nowUtc,
            nowUtc.AddSeconds(options.LeaseSeconds),
            options.MaxAttempts,
            cancellationToken);

        if (claimed)
        {
            _claimsHeld.Add(intent.DedupeKey);
        }

        return claimed;
    }
}

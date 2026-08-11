using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nestly.Application.Notifications;
using Nestly.Domain;
using Nestly.Infrastructure.Options;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// See <see cref="INotificationIntentSweepJob"/> - the path that makes a
/// notification survive the death of the process that was supposed to send it
/// (task 294).
/// </summary>
/// <remarks>
/// <para>
/// <b>It re-runs the handler, it does not re-implement it.</b> The intent
/// carries the serialized domain event, so the sweep rehydrates it and hands
/// it to the same <c>INotificationTriggerHandler</c> the in-process path used.
/// Every repository read, every template variable and every recipient rule is
/// therefore identical on both paths by construction, rather than by two
/// pieces of code agreeing. It addresses the notification handlers directly
/// rather than re-publishing through MediatR precisely so that nothing else
/// subscribed to that event stream - escrow, referrals, auto-assignment - runs
/// a second time.
/// </para>
/// <para>
/// <b>Multi-instance safety.</b> The candidate query confers nothing; the
/// claim does. Two instances sweeping simultaneously will both list the same
/// rows and exactly one of them will win each conditional UPDATE.
/// </para>
/// </remarks>
public class NotificationIntentSweepJob : INotificationIntentSweepJob
{
    private const string ExhaustedReason = "Retry bound reached without a successful dispatch.";

    private readonly INotificationIntentRepository _repository;
    private readonly INotificationIntentCoordinator _coordinator;
    private readonly IEnumerable<INotificationTriggerHandler> _handlers;
    private readonly IOptionsMonitor<NotificationIntentOptions> _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<NotificationIntentSweepJob> _logger;

    public NotificationIntentSweepJob(
        INotificationIntentRepository repository,
        INotificationIntentCoordinator coordinator,
        IEnumerable<INotificationTriggerHandler> handlers,
        IOptionsMonitor<NotificationIntentOptions> options,
        TimeProvider timeProvider,
        ILogger<NotificationIntentSweepJob> logger)
    {
        _repository = repository;
        _coordinator = coordinator;
        _handlers = handlers;
        _options = options;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<int> SweepAsync(CancellationToken cancellationToken = default)
    {
        var options = _options.CurrentValue;
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;

        // Terminal state first, so an exhausted intent cannot be selected as a
        // candidate by this same pass.
        var abandoned = await _repository.AbandonExhaustedAsync(nowUtc, options.MaxAttempts, ExhaustedReason, cancellationToken);
        if (abandoned > 0)
        {
            _logger.LogError(
                "Notification intent sweep abandoned {AbandonedCount} intent(s) after {MaxAttempts} attempts - those customers were owed a message and will not receive it.",
                abandoned, options.MaxAttempts);
        }

        var candidates = await _repository.ListSweepableAsync(
            nowUtc,
            nowUtc.AddSeconds(-options.GraceSeconds),
            options.MaxAttempts,
            options.BatchSize,
            cancellationToken);

        var dispatched = 0;

        foreach (var intent in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (await TryDeliverAsync(intent, cancellationToken))
            {
                dispatched++;
            }
        }

        if (dispatched > 0)
        {
            _logger.LogWarning(
                "Notification intent sweep recovered {DispatchedCount} notification(s) the in-process path did not deliver.",
                dispatched);
        }

        return dispatched;
    }

    private async Task<bool> TryDeliverAsync(NotificationIntent intent, CancellationToken cancellationToken)
    {
        var eventType = NotificationIntentPlanner.ResolveEventType(intent.DomainEventType);
        if (eventType is null)
        {
            // Permanent, not transient: no number of retries will make a type
            // that no longer exists resolvable.
            await MarkUndeliverableAsync(intent, $"Unknown domain event type '{intent.DomainEventType}'.", cancellationToken);
            return false;
        }

        var handler = _handlers.FirstOrDefault(candidate => candidate.CanHandle(eventType));
        if (handler is null)
        {
            await MarkUndeliverableAsync(intent, $"No notification trigger handler owns '{intent.DomainEventType}'.", cancellationToken);
            return false;
        }

        if (!await _coordinator.TryClaimForSweepAsync(intent, cancellationToken))
        {
            // Another instance took it between the list and the claim, which
            // is the mechanism working, not a problem.
            return false;
        }

        try
        {
            var domainEvent = DomainEventPayloadSerializer.Deserialize(intent.PayloadJson, eventType);
            if (domainEvent is null)
            {
                await MarkUndeliverableAsync(intent, $"Payload could not be deserialized into '{intent.DomainEventType}'.", cancellationToken);
                return false;
            }

            await handler.HandleAsync(domainEvent, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Notification intent {DedupeKey} ({EventType}) failed on sweep attempt {AttemptCount}.",
                intent.DedupeKey, intent.EventType, intent.AttemptCount + 1);

            // Releases the lease this method took, so the retry happens on the
            // next pass rather than after the lease times out. A handler that
            // threw before it ever reached the coordinator - a repository read
            // failing, which is the original defect this whole feature is
            // about - would otherwise strand its own claim for the full lease.
            await ReleaseAsync(intent, exception.Message, cancellationToken);
            return false;
        }

        // "The handler ran" is not "the message went out". Ask the row, which
        // is the only thing that actually knows.
        var settled = await _repository.GetByDedupeKeyAsync(intent.DedupeKey, cancellationToken);
        if (settled is null || settled.Status == NotificationIntentStatus.Pending)
        {
            _logger.LogWarning(
                "Notification intent {DedupeKey} ({EventType}) was claimed but its handler resolved nothing - releasing it for the next sweep.",
                intent.DedupeKey, intent.EventType);

            await ReleaseAsync(intent, "Handler completed without resolving the intent.", cancellationToken);
            return false;
        }

        return settled.Status == NotificationIntentStatus.Delivered;
    }

    private async Task ReleaseAsync(NotificationIntent intent, string error, CancellationToken cancellationToken)
    {
        try
        {
            await _repository.RecordFailureAsync(intent.DedupeKey, error, cancellationToken);
        }
        catch (Exception releaseFailure)
        {
            // Best effort by necessity - the sweep may be failing precisely
            // because the database is unreachable. The lease expiring is the
            // backstop that needs no write.
            _logger.LogError(
                releaseFailure,
                "Could not release the lease on notification intent {DedupeKey}; it will retry once the lease expires.",
                intent.DedupeKey);
        }
    }

    /// <summary>
    /// A permanent defect in the intent itself. Skipped rather than abandoned:
    /// abandoned means "we tried and could not", and reserving it for that
    /// keeps it usable as an alerting signal.
    /// </summary>
    private async Task MarkUndeliverableAsync(NotificationIntent intent, string reason, CancellationToken cancellationToken)
    {
        _logger.LogError(
            "Notification intent {DedupeKey} ({EventType}) is undeliverable and will not be retried: {Reason}",
            intent.DedupeKey, intent.EventType, reason);

        await _repository.MarkSkippedAsync(intent.DedupeKey, reason, _timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
    }
}

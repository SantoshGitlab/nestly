using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Nestly.Application.Notifications;
using Nestly.BuildingBlocks.Primitives;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Writes the durable notification intents (task 294) <b>inside</b> the
/// transaction that justifies them.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why <c>SavingChanges</c> and not <c>SavedChanges</c>.</b> Its sibling
/// <see cref="DomainEventDispatchInterceptor"/> deliberately runs
/// post-commit, so a handler never reacts to a change that was rolled back.
/// This one has to run pre-commit for the mirror-image reason: the intent row
/// must be part of the same <c>SaveChanges</c> - and the same explicit
/// transaction, where one is open - as the state change, so the two commit or
/// roll back together. That single property is what makes the notification
/// guarantee real rather than a slightly smaller race: there is no instant at
/// which a booking is Confirmed and nothing anywhere records that the customer
/// is owed a message about it.
/// </para>
/// <para>
/// <b>Same aggregate-root-only limitation as dispatch.</b> Events raised on a
/// plain <c>Entity&lt;TId&gt;</c> are invisible here exactly as they are to
/// the dispatcher (docs/ARCHITECTURE.md). That is deliberate: the two sweeps
/// see the same events, so an intent can never be written for a message the
/// in-process path will not also attempt.
/// </para>
/// <para>
/// <b>Events are read, never cleared.</b> Draining them is
/// <see cref="DomainEventDispatchInterceptor"/>'s job, post-commit; clearing
/// them here would leave a committed intent with nothing to dispatch it in
/// process.
/// </para>
/// </remarks>
public sealed class NotificationIntentInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        WriteIntents(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        WriteIntents(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void WriteIntents(DbContext? context)
    {
        if (context is not NestlyDbContext nestlyContext)
        {
            return;
        }

        var pendingEvents = nestlyContext.ChangeTracker
            .Entries<AggregateRoot<Guid>>()
            .SelectMany(entry => entry.Entity.DomainEvents)
            .ToList();

        if (pendingEvents.Count == 0)
        {
            return;
        }

        // Guards the one way a duplicate can arise: two SaveChanges calls on a
        // context whose aggregates still hold their events, which happens
        // whenever DomainEventDispatchInterceptor (the thing that drains them)
        // is not attached. Without this the second save would hit the unique
        // index on dedupe_key and fail an already-valid business operation.
        var alreadyWritten = nestlyContext.ChangeTracker
            .Entries<NotificationIntent>()
            .Select(entry => entry.Entity.DedupeKey)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var domainEvent in pendingEvents)
        {
            foreach (var eventType in NotificationIntentPlanner.Plan(domainEvent))
            {
                var dedupeKey = NotificationIntent.BuildDedupeKey(domainEvent.EventId, eventType);
                if (!alreadyWritten.Add(dedupeKey))
                {
                    continue;
                }

                nestlyContext.NotificationIntents.Add(new NotificationIntent(
                    Guid.NewGuid(),
                    domainEvent.EventId,
                    domainEvent.GetType().Name,
                    DomainEventPayloadSerializer.Serialize(domainEvent),
                    eventType,
                    domainEvent.OccurredOnUtc));
            }
        }
    }
}

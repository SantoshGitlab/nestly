using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>
/// The durable record of a decision to tell somebody something (task 294).
///
/// <para>
/// <b>What this closes.</b> Until this existed, every notification in the
/// system was at-most-once: the state change committed, then an in-process
/// MediatR handler re-read several repositories post-commit and dispatched. A
/// throw or a process death anywhere in that window lost the notification
/// permanently, because nothing had been written down saying it was owed.
/// docs/ARCHITECTURE.md ("DOMAIN EVENT DISPATCH AND DELIVERY") states the rule
/// this type implements: <i>a notification trigger must not depend solely on a
/// post-commit domain event handler that can throw.</i>
/// </para>
///
/// <para>
/// <b>One row per message, not per event.</b> A single
/// <c>BookingStatusChangedEvent</c> reaching Confirmed warrants two messages
/// (BookingConfirmed and PaymentSuccess), and they succeed or fail
/// independently - a crash between the two must lose neither and re-send
/// neither. Giving each its own row, each with its own claim, is what makes
/// that true without any handler having to reason about partial progress.
/// </para>
///
/// <para>
/// <b>Written inside the transaction that justifies it.</b>
/// <c>NotificationIntentInterceptor</c> adds these rows during
/// <c>SavingChanges</c>, so they are part of the same <c>SaveChanges</c> - and
/// the same explicit transaction, where one is open - as the status change,
/// the chat message or the subscription roll-over that warrants them. There is
/// no window in which the state change is durable and the obligation to
/// announce it is not; a rollback discards both.
/// </para>
///
/// <para>
/// <b>What is guaranteed, and what is not.</b> Delivery is now at-least-once
/// up to <see cref="AttemptCount"/>, deduplicated by <see cref="DedupeKey"/>,
/// and terminal at <see cref="NotificationIntentStatus.Abandoned"/>. It is not
/// exactly-once: the claim is taken before the send, so a process death
/// between a successful provider send and <see cref="MarkDelivered"/> leaves
/// the row claimed, and the sweep will re-send it once the lease expires.
/// Duplicate-then-delivered is the failure mode this design chooses over
/// silence, deliberately.
/// </para>
/// </summary>
public class NotificationIntent : Entity<Guid>
{
    /// <summary>
    /// The idempotency key, unique across the table. Deterministic and derived
    /// entirely from the intent - see <see cref="BuildDedupeKey"/> for why it
    /// is built from the domain event's identity rather than from the business
    /// ids or a timestamp.
    /// </summary>
    public string DedupeKey { get; private set; } = string.Empty;

    /// <summary><see cref="BuildingBlocks.Primitives.IDomainEvent.EventId"/> of the occurrence that warranted this message - the correlation handle between the rows a single event produced.</summary>
    public Guid DomainEventId { get; private set; }

    /// <summary>Simple (unqualified) CLR type name of the domain event, resolved back to a type through an explicit allow-list rather than <c>Type.GetType</c> - see <c>NotificationIntentPlanner.ResolveEventType</c>.</summary>
    public string DomainEventType { get; private set; } = string.Empty;

    /// <summary>The serialized domain event, which is all the sweep needs to re-run the same handler the in-process path ran.</summary>
    public string PayloadJson { get; private set; } = string.Empty;

    /// <summary>Which message this row owes. One row per event type, never a set - see the class doc comment.</summary>
    public NotificationEventType EventType { get; private set; }

    public NotificationIntentStatus Status { get; private set; }

    /// <summary>Incremented by the claim, not by the send, so a process that dies mid-send still consumes an attempt and cannot spin forever.</summary>
    public int AttemptCount { get; private set; }

    /// <summary>Stamped from the domain event's own <c>OccurredOnUtc</c> rather than the wall clock at insert time, so the row's age reflects the fact it describes.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? LastAttemptAtUtc { get; private set; }

    /// <summary>When the row reached a terminal state (delivered, skipped or abandoned).</summary>
    public DateTime? CompletedAtUtc { get; private set; }

    /// <summary>Identifies the process/scope currently holding the lease - diagnostic only; correctness comes from the conditional UPDATE in <c>INotificationIntentRepository.TryClaimAsync</c>, not from comparing this.</summary>
    public string? LeaseOwner { get; private set; }

    /// <summary>While in the future, this row is being worked on and no other instance may claim it. An expired lease is indistinguishable from a crashed worker, which is exactly the intent.</summary>
    public DateTime? LeaseExpiresAtUtc { get; private set; }

    public string? LastError { get; private set; }

    /// <summary>Why the row is in its terminal state - the reason a message was deliberately not sent, or the reason the retry bound gave up.</summary>
    public string? Resolution { get; private set; }

    protected NotificationIntent() { }

    public NotificationIntent(
        Guid id,
        Guid domainEventId,
        string domainEventType,
        string payloadJson,
        NotificationEventType eventType,
        DateTime createdAtUtc)
        : base(id)
    {
        DomainEventId = domainEventId == Guid.Empty
            ? throw new ArgumentException("Domain event id is required.", nameof(domainEventId))
            : domainEventId;
        DomainEventType = string.IsNullOrWhiteSpace(domainEventType)
            ? throw new ArgumentException("Domain event type is required.", nameof(domainEventType))
            : domainEventType;
        PayloadJson = string.IsNullOrWhiteSpace(payloadJson)
            ? throw new ArgumentException("Payload is required.", nameof(payloadJson))
            : payloadJson;
        EventType = eventType;
        DedupeKey = BuildDedupeKey(domainEventId, eventType);
        Status = NotificationIntentStatus.Pending;
        AttemptCount = 0;
        CreatedAtUtc = createdAtUtc;
    }

    /// <summary>
    /// The idempotency key: the domain event's id plus the message it owes.
    ///
    /// <para>
    /// <b>Why the event id and not the business identity.</b> A key like
    /// <c>booking:{id}:BookingRescheduled</c> would be deterministic too, and
    /// wrong: a booking may legitimately be rescheduled twice, and the second
    /// notification would collide with the first and be silently swallowed as
    /// a duplicate. The event id names one occurrence, which is exactly the
    /// granularity a message is owed at.
    /// </para>
    ///
    /// <para>
    /// <b>Why not a timestamp.</b> The sweep rebuilds this key from the
    /// deserialized event, so anything that does not survive the round trip
    /// byte-for-byte would produce a different key on the retry path and
    /// defeat the deduplication it exists for. That is also why
    /// <c>DomainEvent.EventId</c> is an <c>init</c> property rather than a
    /// get-only one: it has to come back out of the payload unchanged.
    /// </para>
    /// </summary>
    public static string BuildDedupeKey(Guid domainEventId, NotificationEventType eventType) =>
        $"{domainEventId:N}:{eventType}";

    /// <summary>The message went out. Terminal.</summary>
    public void MarkDelivered(DateTime nowUtc)
    {
        Status = NotificationIntentStatus.Delivered;
        CompletedAtUtc = nowUtc;
        LeaseOwner = null;
        LeaseExpiresAtUtc = null;
    }

    /// <summary>
    /// The message is deliberately not being sent - muted by ops, the
    /// recipient is on a live connection, the row it described is gone.
    /// Terminal, and distinct from delivered so the two are never confused in
    /// an audit.
    /// </summary>
    public void MarkSkipped(string reason, DateTime nowUtc)
    {
        Status = NotificationIntentStatus.Skipped;
        Resolution = reason;
        CompletedAtUtc = nowUtc;
        LeaseOwner = null;
        LeaseExpiresAtUtc = null;
    }

    /// <summary>The retry bound was reached. Terminal, and the only state in which a notification is knowingly given up on - it exists so a permanently failing intent stops consuming sweeps instead of being retried forever.</summary>
    public void MarkAbandoned(string reason, DateTime nowUtc)
    {
        Status = NotificationIntentStatus.Abandoned;
        Resolution = reason;
        CompletedAtUtc = nowUtc;
        LeaseOwner = null;
        LeaseExpiresAtUtc = null;
    }

    /// <summary>Takes the lease. Never call this directly on a loaded entity for concurrency control - <c>INotificationIntentRepository.TryClaimAsync</c> does the same thing as one conditional UPDATE, which is what makes it safe across instances.</summary>
    public void Claim(string owner, DateTime nowUtc, DateTime leaseExpiresAtUtc)
    {
        AttemptCount += 1;
        LastAttemptAtUtc = nowUtc;
        LeaseOwner = owner;
        LeaseExpiresAtUtc = leaseExpiresAtUtc;
    }

    /// <summary>Records why an attempt failed and drops the lease so the next sweep can retry immediately rather than waiting it out.</summary>
    public void RecordFailure(string error)
    {
        LastError = error;
        LeaseOwner = null;
        LeaseExpiresAtUtc = null;
    }
}

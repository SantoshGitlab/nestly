using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain.Events;

public sealed record BookingCreatedEvent(Guid BookingId, Guid CustomerId) : DomainEvent;

public sealed record BookingStatusChangedEvent(Guid BookingId, BookingStatus FromStatus, BookingStatus ToStatus) : DomainEvent;

// --- Task 272: the order-tracking event family ---
//
// All five live here rather than in per-aggregate files (the convention the
// rest of this folder follows) because they are one family with one consumer:
// the live tracking screen behind a single booking. Two of them are raised by
// entities other than Booking - BookingProviderAssignment and
// ProviderLocationPing - but splitting them across three files would scatter
// a set that is only ever read, subscribed to and reasoned about together.
//
// PII RULE: none of these payloads may carry a customer or provider name,
// phone number, email or address. Broadcast handlers (task 274) push them
// straight to browsers over SignalR, so anything in a payload is on the wire.
// Ids and coordinates only; a handler that genuinely needs a name looks it up
// against its own authorization rules (task 275's masked-phone response).
//
// DURABILITY: dispatch is in-process MediatR, post-commit, with no outbox -
// see DomainEventDispatchInterceptor and the "DOMAIN EVENT DISPATCH AND
// DELIVERY" section of docs/ARCHITECTURE.md. A dropped tracking broadcast is
// acceptable (REST re-read is the source of truth); a dropped notification is
// not, so no notification trigger may hang off these events alone.

/// <summary>
/// The provider accepted an outstanding assignment (task 272) - raised by
/// <see cref="BookingProviderAssignment.Accept"/>, which previously raised
/// nothing at all, leaving nothing in the system able to react to the single
/// moment the whole tracking chain waits on.
/// </summary>
/// <param name="AssignmentId">The accepted <see cref="BookingProviderAssignment"/>.</param>
/// <param name="BookingId">The booking the assignment is for.</param>
/// <param name="ProviderId">The accepting provider - an id only; the display name/photo a UI wants are looked up by the reader.</param>
/// <param name="AcceptedAtUtc">
/// The assignment's own <see cref="BookingProviderAssignment.RespondedAt"/>,
/// carried explicitly rather than left to <see cref="DomainEvent.OccurredOnUtc"/>:
/// the two agree today, but the entity's stamp is the one that is persisted
/// and the one a later read will compare against.
/// </param>
public sealed record ProviderAssignmentAcceptedEvent(
    Guid AssignmentId,
    Guid BookingId,
    Guid ProviderId,
    DateTime AcceptedAtUtc) : DomainEvent;

/// <summary>
/// The provider set off for the customer's address - raised by
/// <see cref="Booking.TransitionTo"/> on entry to
/// <see cref="BookingStatus.ProviderEnRoute"/> (task 264's state), so task
/// 270's en-route endpoint gets it without having to remember to raise it.
/// </summary>
/// <remarks>
/// Deliberately a companion to <see cref="BookingStatusChangedEvent"/>, not a
/// replacement: that event still fires for the same transition and remains
/// the stream that lifecycle-wide handlers (metrics, escrow, referrals)
/// subscribe to. A handler must pick one of the two, never both, or it will
/// run twice per transition.
/// </remarks>
/// <param name="ProviderId">
/// <see cref="Booking.AssignedProviderId"/> at the moment of the transition.
/// Nullable because that field is nullable - ProviderEnRoute is only reachable
/// from Assigned, so in practice it is always set, but the booking aggregate
/// carries no invariant tying the two together and this event does not invent
/// one.
/// </param>
public sealed record ProviderEnRouteEvent(Guid BookingId, Guid? ProviderId) : DomainEvent;

/// <summary>
/// The provider reached the customer's address but has not started work -
/// raised by <see cref="Booking.TransitionTo"/> on entry to
/// <see cref="BookingStatus.ProviderArrived"/>. Same companion-to-
/// <see cref="BookingStatusChangedEvent"/> relationship as
/// <see cref="ProviderEnRouteEvent"/>.
/// </summary>
public sealed record ProviderArrivedEvent(Guid BookingId, Guid? ProviderId) : DomainEvent;

/// <summary>
/// A new position fix landed on a provider's trail - raised by the
/// <see cref="ProviderLocationPing"/> constructor (task 268's append-only
/// entity), so task 269's ingest endpoint gets it purely by appending a ping.
/// </summary>
/// <remarks>
/// Carries the fix itself so the tracking broadcast (task 274) can push
/// coordinates without re-reading the trail it was just handed. This is the
/// payload most exposed to browsers, so it is also the one that must stay
/// free of anything identifying beyond ids.
/// </remarks>
/// <param name="BookingId">The job this fix belongs to, or null for an idle/on-shift fix that no booking's tracking group should ever see.</param>
/// <param name="RecordedAtUtc">
/// The device's own stamp, not the server's - a reader deciding whether to
/// draw this fix needs to know how old it is, and
/// <see cref="DomainEvent.OccurredOnUtc"/> only says when the row was built.
/// </param>
public sealed record ProviderLocationUpdatedEvent(
    Guid PingId,
    Guid ProviderId,
    Guid? BookingId,
    decimal Latitude,
    decimal Longitude,
    decimal? AccuracyMetres,
    DateTime RecordedAtUtc) : DomainEvent;

/// <summary>
/// A booking's customer-facing arrival estimate was recomputed.
/// </summary>
/// <remarks>
/// <b>Not raised anywhere yet.</b> Its source is task 271's ETA computation
/// and the tracking row that stores EtaSeconds/EtaComputedAtUtc/EtaSource,
/// neither of which exists on this branch - the type is added here, with the
/// rest of its family, so task 271 raises it rather than inventing a second
/// shape and task 274 can wire the "EtaUpdated" broadcast against a fixed
/// contract. Nothing subscribes to it today.
/// <para>
/// The ETA's provenance (<c>RouteEstimateSource</c>: sandbox vs Google) is
/// deliberately absent: that enum lives in <c>Nestly.Application.Routing</c>
/// and Domain must not depend on Application. It is support-facing rather
/// than customer-facing anyway, and task 271 persists it on the tracking row
/// where a support agent reads it.
/// </para>
/// </remarks>
/// <param name="EtaSeconds">Estimated remaining travel time in seconds.</param>
/// <param name="EtaComputedAtUtc">When the estimate was produced - an ETA without its computation time is unreadable a minute later.</param>
public sealed record BookingEtaUpdatedEvent(
    Guid BookingId,
    Guid? ProviderId,
    int EtaSeconds,
    DateTime EtaComputedAtUtc) : DomainEvent;

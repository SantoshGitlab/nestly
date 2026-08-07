using Nestly.Domain;

namespace Nestly.Application.Tracking;

// --- Task 274: the wire format of the live tracking hub ---
//
// These three records ARE the wire. Whatever sits on one of them is in a
// browser, in that browser's memory, and in whatever the client logs. So:
// ids, coordinates, enums and timestamps only - never a customer or provider
// name, mobile number, email or address. This is the same rule the events
// themselves declare (see the PII RULE comment in
// Nestly.Domain.Events.BookingEvents), restated here because this is the layer
// where it actually becomes irreversible.
//
// They are deliberately NOT the domain events serialised directly. The events
// are in-process types and may legitimately grow fields for in-process
// consumers that have more right to data than a browser does -
// ProviderLocationUpdatedEvent already carries PingId, ProviderId and
// AccuracyMetres that no watcher needs. Copying into a narrower record makes
// widening the wire an explicit edit to this one file, which
// BookingTrackingBroadcastTests' reflection guard then fails on.
//
// Every one of them carries BookingId. That is not a PII concession: SignalR
// does not tell a client which group a frame arrived from, so a connection
// watching two bookings (an admin console, a provider with a queue) cannot
// otherwise tell the frames apart. It is the one routing field a client
// cannot derive, and it is an opaque id the client already holds.

/// <summary>
/// A booking moved between lifecycle states (<c>BookingStatusChanged</c>).
/// Both ends of the transition, because a tracking UI animates the move and
/// needs to know whether it missed a step.
/// </summary>
public sealed record BookingStatusChangedBroadcast(
    Guid BookingId,
    BookingStatus FromStatus,
    BookingStatus ToStatus);

/// <summary>
/// A new position fix for the provider working this booking
/// (<c>ProviderLocationUpdated</c>).
/// </summary>
/// <remarks>
/// The narrowest payload in the set, and the one the PII rule was written for:
/// latitude, longitude and the fix's own timestamp, and nothing else. In
/// particular <c>ProviderId</c> and <c>PingId</c> from
/// <see cref="Nestly.Domain.Events.ProviderLocationUpdatedEvent"/> are dropped
/// - a customer watching a map has no use for either, and a provider id is a
/// handle other endpoints will resolve to a person. <c>AccuracyMetres</c> is
/// dropped too, for a different reason: it is null for most devices and a map
/// marker that silently stops drawing its accuracy halo is worse than one that
/// never drew it.
/// </remarks>
/// <param name="RecordedAtUtc">The device's stamp, not the server's - the client draws a marker as stale off this.</param>
public sealed record ProviderLocationBroadcast(
    Guid BookingId,
    decimal Latitude,
    decimal Longitude,
    DateTime RecordedAtUtc);

/// <summary>
/// The booking's arrival estimate was recomputed (<c>EtaUpdated</c>).
/// </summary>
/// <remarks>
/// Carries the computation time as well as the number: an ETA without it is
/// unreadable a minute later, and the client has no other way to age it.
/// <c>EtaSource</c> is deliberately absent - it is support-facing provenance
/// that task 271 persists on the tracking row, and it tells a customer nothing
/// while telling anyone watching the socket which routing vendor we use.
/// </remarks>
public sealed record BookingEtaBroadcast(
    Guid BookingId,
    int EtaSeconds,
    DateTime EtaComputedAtUtc);

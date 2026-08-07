using Nestly.Application.Bookings;
using Nestly.BuildingBlocks.Privacy;
using Nestly.Domain;

namespace Nestly.Application.Tracking;

/// <summary>
/// The provider as the live tracking screen shows them (task 275): the same
/// identity <see cref="BookingProviderSummary"/> puts on the booking detail,
/// plus the one thing that only makes sense while someone is on their way -
/// a way to reach them.
///
/// <paramref name="MaskedPhone"/> is masked at the boundary, not stored
/// masked, and there is deliberately no sibling field carrying the raw
/// number: masking a value while shipping the original beside it is the
/// classic way this control gets defeated. A customer needs to recognise the
/// number calling them, not to be able to dial the provider out of band -
/// and the response is what an attacker with a stolen access token reads, so
/// the number a screen shows and the number in the payload have to be the
/// same masked one.
///
/// Near-duplicate of BookingProviderSummary on purpose: keeping the phone off
/// that record is what stops the masked number leaking onto every general
/// booking read, and nesting the shared three inside a wrapper would make the
/// JSON worse for no gain. <see cref="From"/> builds from the shared mapping
/// so the two can only ever disagree about the phone.
/// </summary>
public sealed record TrackedProviderSummary(string DisplayName, string? PhotoUrl, double? Rating, string? MaskedPhone)
{
    public static TrackedProviderSummary From(Provider provider)
    {
        var identity = BookingProviderSummary.From(provider);
        return new TrackedProviderSummary(
            identity.DisplayName,
            identity.PhotoUrl,
            identity.Rating,
            ContactMasking.MaskOrNull(provider.Phone));
    }
}

/// <summary>
/// Where the provider was at <paramref name="RecordedAtUtc"/>. Field-for-field
/// the same as <see cref="ProviderLocationBroadcast"/> minus its BookingId
/// (redundant once nested under a response that already names the booking),
/// so a screen can render a socket frame and a REST read through one code
/// path.
///
/// RecordedAtUtc is the device clock, matching the broadcast, and it is here
/// rather than implied because a fix that is ten minutes old must be drawn
/// differently from a fresh one. AccuracyMetres, the ping id and the provider
/// id stay off this shape for the same reason task 274 dropped them from the
/// broadcast.
/// </summary>
public sealed record TrackedLocation(decimal Latitude, decimal Longitude, DateTime RecordedAtUtc);

/// <summary>
/// The current estimate, or absent entirely when none has been computed.
/// Field-for-field <see cref="BookingEtaBroadcast"/> minus its BookingId,
/// and like it deliberately without <c>EtaSource</c>: the routing vendor is
/// support-facing provenance that tells a customer nothing while telling
/// anyone reading the response which vendor this system pays.
/// </summary>
public sealed record TrackedEta(int EtaSeconds, DateTime EtaComputedAtUtc);

/// <summary>
/// Where the provider is heading - the booking's own immutable address
/// snapshot, not the customer's current address record, so the pin cannot
/// move mid-job because a profile was edited. Coordinates only: the tracking
/// screen already knows the address it booked, and the street/contact
/// snapshot belongs to the booking detail, not to a PII-bounded live feed.
/// </summary>
public sealed record TrackedDestination(decimal Latitude, decimal Longitude);

/// <summary>
/// Everything a live tracking screen needs and nothing else (task 275).
///
/// This is a deliberately narrow read, not a booking read with tracking
/// bolted on: it is the response most exposed to a stolen customer token,
/// because it is the one polled continuously while a stranger is en route to
/// a home address. So it carries no price, no timeline, no coupon, no
/// customer detail, no provider id, no email, and no raw phone number -
/// nothing that widens what one leaked token is worth. Anything a screen
/// needs beyond this belongs on the booking detail, behind its own read.
///
/// Every field except <paramref name="Destination"/> and the status pair is
/// nullable, and all three of them go missing for ordinary reasons: a
/// provider assigned but not yet moving has no location, an ETA is absent
/// until the first route lookup, and a provider withdrawn mid-window leaves
/// the summary null. A tracking screen must render all of those, so none of
/// them is an error.
/// </summary>
public sealed record BookingTrackingResponse(
    Guid BookingId,
    BookingStatus Status,
    string StatusLabel,
    TrackedProviderSummary? Provider,
    TrackedLocation? ProviderLocation,
    TrackedEta? Eta,
    TrackedDestination Destination);

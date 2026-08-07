using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Nestly.Infrastructure.Realtime;

/// <summary>
/// Real-time transport for end-to-end order tracking (task 273) - booking-group
/// membership only. Exactly the shape <see cref="ChatHub"/> proved out: the hub
/// never mutates anything and has no "send" RPC, because every payload it
/// carries (status changes, provider location, ETA) is produced by a REST call
/// or a background computation elsewhere, persisted there, and pushed from a
/// domain-event handler (task 274). A client that never opens a socket still
/// gets the same data by polling the tracking endpoint.
///
/// One hub TYPE, mapped at <see cref="HubRoutes.TrackingPath"/> by all three
/// APIs - consumer-api (customer JWT), provider-api (provider JWT) and
/// admin-api (admin JWT), each the default scheme in its own process, so the
/// bare <c>[Authorize]</c> resolves correctly in all three without this class
/// naming a scheme. That single type is required, not tidiness: SignalR's
/// Redis backplane fans a group message out to every server subscribed under
/// the hub's type name, and the whole point of this feature is cross-process -
/// the provider's location ping is ingested by provider-api and has to reach a
/// customer connection held by consumer-api. Three per-API hub classes would
/// be three disconnected group namespaces.
///
/// Authorization is deliberately not on the connection but on the join:
/// holding a valid token gets you a socket, it does not get you a booking. See
/// <see cref="BookingTrackingAuthorizer"/> for the three-sided rule.
/// </summary>
[Authorize]
public sealed class BookingTrackingHub : Hub
{
    /// <summary>
    /// The single denial message for every refused join. One message for
    /// "no such booking", "not yours" and "no longer trackable" alike, so a
    /// caller cannot use the difference to learn that a booking id exists -
    /// the hub equivalent of the 404-not-403 rule the booking read endpoints
    /// follow (a hub has no status code to vary, only this text).
    /// </summary>
    public const string AccessDeniedMessage = "Tracking is not available for this booking.";

    private readonly BookingTrackingAuthorizer _authorizer;
    private readonly ILogger<BookingTrackingHub> _logger;

    public BookingTrackingHub(BookingTrackingAuthorizer authorizer, ILogger<BookingTrackingHub> logger)
    {
        _authorizer = authorizer;
        _logger = logger;
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (exception is not null)
        {
            _logger.LogDebug(exception, "Tracking connection {ConnectionId} closed with an error.", Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Joins this connection to the booking's tracking group, after verifying
    /// access - the owning customer while the booking is trackable, the
    /// provider on its live assignment, or an admin holding
    /// <see cref="BookingTrackingAuthorizer.AdminPermissionCode"/>.
    /// </summary>
    public async Task JoinBooking(Guid bookingId)
    {
        if (!await _authorizer.CanTrackAsync(Context.User, bookingId))
        {
            // Booking id and connection id only - no claim values, no name,
            // no mobile number (docs/CODING-STANDARDS.md LOGGING).
            _logger.LogWarning(
                "Denied tracking join for booking {BookingId} on connection {ConnectionId}.",
                bookingId, Context.ConnectionId);

            throw new HubException(AccessDeniedMessage);
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, TrackingGroups.Booking(bookingId));
    }

    /// <summary>
    /// Leaves the booking's tracking group. Unauthorized deliberately: leaving
    /// a group you were never in is a no-op, and refusing it would tell a
    /// caller something about the booking that <see cref="JoinBooking"/> is
    /// careful not to.
    /// </summary>
    public async Task LeaveBooking(Guid bookingId) =>
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, TrackingGroups.Booking(bookingId));
}

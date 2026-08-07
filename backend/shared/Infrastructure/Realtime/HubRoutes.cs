namespace Nestly.Infrastructure.Realtime;

/// <summary>
/// Every SignalR hub route in this codebase (task 190's chat hub, task 273's
/// tracking hub). One class rather than one per hub because
/// <see cref="HubJwtEvents"/> has to recognise a hub request generically -
/// see its doc comment - so the set of hub paths, and the prefix they all
/// share, is a single fact that belongs in a single place.
///
/// <see cref="Prefix"/> is load-bearing, not decorative: a new hub mapped
/// anywhere outside it would silently lose <c>?access_token=</c> handshake
/// authentication.
/// </summary>
public static class HubRoutes
{
    /// <summary>The path segment every hub is mapped under.</summary>
    public const string Prefix = "/hubs";

    /// <summary>Chat (task 190) - mapped by consumer-api and admin-api.</summary>
    public const string ChatPath = Prefix + "/chat";

    /// <summary>Live order tracking (task 273) - mapped by all three APIs.</summary>
    public const string TrackingPath = Prefix + "/tracking";
}

namespace Nestly.Infrastructure.Realtime;

/// <summary>
/// Route(s) <see cref="ChatHub"/> is mapped at (task 190). One path shared by
/// both consumer-api and admin-api - see ChatHub's doc comment for why this
/// is a single hub type reused by both processes rather than two.
/// </summary>
public static class ChatHubRoutes
{
    public const string ChatPath = "/hubs/chat";
}

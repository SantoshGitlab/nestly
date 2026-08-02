using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Nestly.Infrastructure.Realtime;

/// <summary>
/// JWT-over-SignalR (task 190): a browser cannot set the <c>Authorization</c>
/// header on a WebSocket handshake, so the SignalR JS client instead sends
/// the access token as an <c>access_token</c> query-string parameter on the
/// connection URL (the standard client behavior documented by ASP.NET
/// Core's own SignalR authentication guidance). The bearer handler only
/// reads the Authorization header by default, so without this event a chat
/// hub connection would look unauthenticated even with a valid token
/// attached. Scoped to <see cref="ChatHubRoutes.ChatPath"/> only - every
/// other endpoint on these APIs keeps using the Authorization header exactly
/// as before, so a token leaking into server access logs via the query
/// string is a risk accepted for the hub path alone, not project-wide.
/// </summary>
public static class ChatHubJwtEvents
{
    public static JwtBearerEvents Create() => new()
    {
        OnMessageReceived = context =>
        {
            StringValues accessToken = context.Request.Query["access_token"];
            PathString path = context.HttpContext.Request.Path;

            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments(ChatHubRoutes.ChatPath))
            {
                context.Token = accessToken;
            }

            return Task.CompletedTask;
        }
    };
}

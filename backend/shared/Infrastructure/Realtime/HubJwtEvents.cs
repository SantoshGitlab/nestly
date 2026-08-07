using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Nestly.Infrastructure.Realtime;

/// <summary>
/// JWT-over-SignalR (tasks 190, 273): a browser cannot set the
/// <c>Authorization</c> header on a WebSocket handshake, so the SignalR JS
/// client instead sends the access token as an <c>access_token</c>
/// query-string parameter on the connection URL (the standard client
/// behavior documented by ASP.NET Core's own SignalR authentication
/// guidance). The bearer handler only reads the Authorization header by
/// default, so without this event a hub connection would look
/// unauthenticated even with a valid token attached.
///
/// Task 273 widened the predicate from the chat path alone to
/// <see cref="HubRoutes.Prefix"/>: it was written when chat was the only hub,
/// and left every hub added afterwards (starting with
/// <see cref="BookingTrackingHub"/>) unable to authenticate its handshake at
/// all. It is deliberately still a prefix test and not "any request with an
/// access_token query parameter" - a query-string token can leak into server
/// access logs, browser history and Referer headers, so it stays confined to
/// the hub routes that have no alternative. Every REST endpoint on these APIs
/// keeps using the Authorization header exactly as before.
///
/// <see cref="PathString.StartsWithSegments(PathString)"/> matches on whole
/// segments, so "/hubs/tracking" is covered while a look-alike path such as
/// "/hubsomething" is not.
/// </summary>
public static class HubJwtEvents
{
    public static JwtBearerEvents Create() => new()
    {
        OnMessageReceived = context =>
        {
            StringValues accessToken = context.Request.Query["access_token"];
            PathString path = context.HttpContext.Request.Path;

            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments(HubRoutes.Prefix))
            {
                context.Token = accessToken;
            }

            return Task.CompletedTask;
        }
    };
}

using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Nestly.Infrastructure.Realtime;

namespace Nestly.Catalog.Tests;

/// <summary>
/// Task 273 fix (a): the <c>?access_token=</c> lift used to be scoped to the
/// chat path alone, which left every hub added afterwards unable to
/// authenticate a WebSocket handshake. These assert the widened predicate on
/// both sides - it must cover every hub route, and must NOT start honouring a
/// query-string token on ordinary API routes (that is auth-relevant: a token
/// in a query string ends up in access logs and Referer headers, so it stays
/// confined to the handshakes that have no header alternative).
/// </summary>
public sealed class HubJwtEventsTests
{
    private static readonly JwtBearerEvents Events = HubJwtEvents.Create();

    private static async Task<string?> TokenLiftedFrom(string path, string queryString = "?access_token=the-token")
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = path;
        httpContext.Request.QueryString = new QueryString(queryString);

        var scheme = new AuthenticationScheme(
            JwtBearerDefaults.AuthenticationScheme, null, typeof(JwtBearerHandler));
        var context = new MessageReceivedContext(httpContext, scheme, new JwtBearerOptions());

        await Events.MessageReceived(context);
        return context.Token;
    }

    [Theory]
    [InlineData(HubRoutes.ChatPath)]
    [InlineData(HubRoutes.TrackingPath)]
    [InlineData(HubRoutes.Prefix)]
    // The SignalR client appends its own segments to the hub route
    // (negotiate) before it ever opens the socket.
    [InlineData(HubRoutes.TrackingPath + "/negotiate")]
    // A hub route this codebase has not invented yet must not need a second
    // edit here - that is the whole point of a prefix predicate.
    [InlineData(HubRoutes.Prefix + "/some-future-hub")]
    public async Task Lifts_the_query_string_token_for_every_hub_route(string path) =>
        (await TokenLiftedFrom(path)).Should().Be("the-token");

    [Theory]
    [InlineData("/api/v1/bookings")]
    [InlineData("/api/v1/jobs/00000000-0000-0000-0000-000000000001/location")]
    [InlineData("/health/ready")]
    [InlineData("/metrics")]
    [InlineData("/")]
    // Segment-wise matching, not a bare string prefix: a look-alike path is
    // not a hub path.
    [InlineData("/hubsomething")]
    [InlineData("/hubs-admin/tracking")]
    public async Task Ignores_the_query_string_token_outside_the_hub_prefix(string path) =>
        (await TokenLiftedFrom(path)).Should().BeNull();

    [Fact]
    public async Task Leaves_the_token_unset_on_a_hub_route_with_no_access_token_parameter() =>
        (await TokenLiftedFrom(HubRoutes.TrackingPath, queryString: string.Empty)).Should().BeNull();

    [Fact]
    public async Task Leaves_the_token_unset_on_a_hub_route_with_an_empty_access_token_parameter() =>
        (await TokenLiftedFrom(HubRoutes.TrackingPath, queryString: "?access_token=")).Should().BeNull();

    [Fact]
    public void Every_hub_route_sits_under_the_prefix_the_predicate_tests()
    {
        // Guards the other direction: a hub mapped outside the prefix would
        // pass every test above and still be unauthenticatable in production.
        HubRoutes.ChatPath.Should().StartWith(HubRoutes.Prefix + "/");
        HubRoutes.TrackingPath.Should().StartWith(HubRoutes.Prefix + "/");
    }
}

using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nestly.Application.Abstractions.Caching;
using Nestly.Application.Routing;
using Nestly.Infrastructure.Options;
using Nestly.Infrastructure.Services;

namespace Nestly.Catalog.Tests;

/// <summary>
/// Covers task 286: the Google Maps integration itself, with no network at
/// all. Every test drives <see cref="GoogleMapsRouteEstimateProvider"/> through
/// a <see cref="StubHttpMessageHandler"/> supplied by a stubbed
/// <see cref="IHttpClientFactory"/> - which is the reason the production
/// registration uses a <i>named</i> client rather than a typed one.
///
/// Three things are being pinned here, and they are the three things that can
/// only fail in production:
/// <list type="number">
/// <item>the outbound request shape the Routes API actually expects
/// (<c>computeRouteMatrix</c>, batched destinations, metric units, and the key
/// in the <c>X-Goog-Api-Key</c> header rather than the URI);</item>
/// <item>key hygiene - the credential must not appear in a URI, a log line, an
/// exception or a cache key, because all four are places a secret outlives the
/// process that held it;</item>
/// <item>the contract in <see cref="IRouteEstimateProvider"/>: exactly one
/// estimate per destination, index-aligned, always, under every failure mode,
/// and never a thrown exception except the caller's own cancellation. A
/// tracking screen showing an approximate ETA is an acceptable outcome; a
/// failed booking is not.</item>
/// </list>
///
/// Note on the wire format: this is the <b>Routes API</b>
/// (<c>computeRouteMatrix</c>), not the legacy Distance Matrix API - Google
/// made the latter legacy on 1 March 2025 and it cannot be enabled on a new
/// Cloud project. So there are no <c>OK</c>/<c>ZERO_RESULTS</c>/<c>NOT_FOUND</c>
/// element strings to parse; a Routes element carries a <c>condition</c> and a
/// <c>google.rpc.Status</c>, and those are what these tests assert on.
/// </summary>
public sealed class GoogleMapsRouteEstimateProviderTests
{
    /// <summary>
    /// Shaped like a real key so a leak test cannot pass by accident on a
    /// value that would never survive a substring search anyway.
    /// </summary>
    private const string ApiKey = "AIzaSyC286-not-a-real-key-9f3b1c7d5e2a4680";

    private const string ComputeRouteMatrixUri = "https://routes.googleapis.com/distanceMatrix/v2:computeRouteMatrix";
    private const string ApiKeyHeaderName = "X-Goog-Api-Key";
    private const string FieldMaskHeaderName = "X-Goog-FieldMask";

    // Koramangala, Bengaluru, and three points north-west of it.
    private static readonly GeoCoordinate Origin = new(12.9352m, 77.6245m);
    private static readonly GeoCoordinate FirstDestination = new(12.9400m, 77.6100m);
    private static readonly GeoCoordinate SecondDestination = new(12.9500m, 77.6000m);
    private static readonly GeoCoordinate ThirdDestination = new(12.9600m, 77.5900m);

    private static readonly GeoCoordinate[] ThreeDestinations = [FirstDestination, SecondDestination, ThirdDestination];

    /// <summary>Everything the provider needs, kept together so a test can reach the cache and the log it was given.</summary>
    private sealed record Harness(
        GoogleMapsRouteEstimateProvider Provider,
        StubHttpMessageHandler Handler,
        StubHttpClientFactory ClientFactory,
        InMemoryCacheService Cache,
        RecordingLogger<GoogleMapsRouteEstimateProvider> Logger);

    private static Harness Build(
        StubHttpMessageHandler handler,
        GoogleMapsOptions? options = null,
        InMemoryCacheService? cache = null)
    {
        var clientFactory = new StubHttpClientFactory(handler);
        var resolvedCache = cache ?? new InMemoryCacheService();
        var logger = new RecordingLogger<GoogleMapsRouteEstimateProvider>();
        var sandbox = new SandboxRouteEstimateProvider(Options.Create(new SandboxRouteEstimateOptions()));

        var provider = new GoogleMapsRouteEstimateProvider(
            clientFactory,
            resolvedCache,
            sandbox,
            Options.Create(options ?? new GoogleMapsOptions { ApiKey = ApiKey }),
            logger);

        return new Harness(provider, handler, clientFactory, resolvedCache, logger);
    }

    /// <summary>The sandbox's own answer for one leg, for tests that assert a fallback produced exactly it.</summary>
    private static RouteEstimate SandboxEstimateFor(GeoCoordinate destination, int destinationIndex) =>
        new SandboxRouteEstimateProvider(Options.Create(new SandboxRouteEstimateOptions()))
            .Estimate(Origin, destination, destinationIndex);

    private static string Matrix(params string[] elements) => "[" + string.Join(",", elements) + "]";

    private static string RouteExists(int destinationIndex, int distanceMetres, string duration) =>
        $$"""
        {"originIndex":0,"destinationIndex":{{destinationIndex}},"distanceMeters":{{distanceMetres}},"duration":"{{duration}}","condition":"ROUTE_EXISTS"}
        """;

    private static JsonNode BodyOf(RecordedHttpRequest request) =>
        JsonNode.Parse(request.Body) ?? throw new InvalidOperationException("The request carried no JSON body.");

    private static (decimal Latitude, decimal Longitude) LatLngAt(JsonNode body, string collection, int index)
    {
        var latLng = body[collection]!.AsArray()[index]!["waypoint"]!["location"]!["latLng"]!;
        return (latLng["latitude"]!.GetValue<decimal>(), latLng["longitude"]!.GetValue<decimal>());
    }

    // ---------------------------------------------------------------------
    // Request shape
    // ---------------------------------------------------------------------

    /// <summary>
    /// One POST for the whole candidate set, not one per destination. This is
    /// the difference between one billed round trip per booking and N of them
    /// on a path that must not hold up a booking.
    /// </summary>
    [Fact]
    public async Task EstimateAsync_posts_one_compute_route_matrix_request_with_every_destination_batched()
    {
        var harness = Build(StubHttpMessageHandler.RespondingWithJson(Matrix(
            RouteExists(0, 4_100, "600s"),
            RouteExists(1, 6_200, "900s"),
            RouteExists(2, 8_300, "1200s"))));

        await harness.Provider.EstimateAsync(Origin, ThreeDestinations);

        harness.Handler.Requests.Should().ContainSingle("three destinations are one batched request, not three requests");
        var request = harness.Handler.Requests[0];
        request.Method.Should().Be(HttpMethod.Post);
        request.RequestUri.Should().Be(new Uri(ComputeRouteMatrixUri));

        var body = BodyOf(request);
        body["origins"]!.AsArray().Should().ContainSingle("the whole batching design is one origin to many destinations");
        LatLngAt(body, "origins", 0).Should().Be((Origin.Latitude, Origin.Longitude));

        body["destinations"]!.AsArray().Should().HaveCount(3);
        LatLngAt(body, "destinations", 0).Should().Be((FirstDestination.Latitude, FirstDestination.Longitude));
        LatLngAt(body, "destinations", 1).Should().Be((SecondDestination.Latitude, SecondDestination.Longitude));
        LatLngAt(body, "destinations", 2).Should().Be((ThirdDestination.Latitude, ThirdDestination.Longitude));
    }

    /// <summary>
    /// Metric units and a traffic-aware drive. Units matter because the whole
    /// system stores metres and this platform is metric; TRAFFIC_AWARE matters
    /// because a free-flow ETA on a congested city road is the exact number
    /// this feature exists to stop showing.
    /// </summary>
    [Fact]
    public async Task EstimateAsync_asks_for_a_metric_traffic_aware_drive()
    {
        var harness = Build(StubHttpMessageHandler.RespondingWithJson(Matrix(RouteExists(0, 4_100, "600s"))));

        await harness.Provider.EstimateAsync(Origin, [FirstDestination]);

        var body = BodyOf(harness.Handler.Requests[0]);
        body["units"]!.GetValue<string>().Should().Be("METRIC");
        body["travelMode"]!.GetValue<string>().Should().Be("DRIVE");
        body["routingPreference"]!.GetValue<string>().Should().Be(
            "TRAFFIC_AWARE",
            "an ETA the customer watches needs live traffic, but not at TRAFFIC_AWARE_OPTIMAL's price and tighter element ceiling");
    }

    /// <summary>
    /// The Routes API takes the credential in a header. Distance Matrix took
    /// it in the query string; moving it into a header is the single change
    /// that keeps it out of request-URI logs and exception messages, so it is
    /// worth an explicit test rather than trusting a code read.
    /// </summary>
    [Fact]
    public async Task EstimateAsync_sends_the_api_key_in_the_header_the_routes_api_expects()
    {
        var harness = Build(StubHttpMessageHandler.RespondingWithJson(Matrix(RouteExists(0, 4_100, "600s"))));

        await harness.Provider.EstimateAsync(Origin, [FirstDestination]);

        harness.Handler.Requests[0].Header(ApiKeyHeaderName).Should().Be(ApiKey);
        harness.ClientFactory.RequestedClientNames.Should().Equal(
            [GoogleMapsRouteEstimateProvider.HttpClientName],
            "the named registration is what carries the configured timeout");
    }

    /// <summary>
    /// Routes API bills partly by the fields returned, so the field mask must
    /// stay narrowed to what the parser actually reads. A reviewer widening it
    /// to a wildcard would raise the bill without changing a single behaviour.
    /// </summary>
    [Fact]
    public async Task EstimateAsync_requests_only_the_response_fields_it_reads()
    {
        var harness = Build(StubHttpMessageHandler.RespondingWithJson(Matrix(RouteExists(0, 4_100, "600s"))));

        await harness.Provider.EstimateAsync(Origin, [FirstDestination]);

        string? fieldMask = harness.Handler.Requests[0].Header(FieldMaskHeaderName);
        fieldMask.Should().NotBeNull();
        fieldMask!.Split(',').Should().BeEquivalentTo(
            ["originIndex", "destinationIndex", "distanceMeters", "duration", "condition", "status"]);
        fieldMask.Should().NotContain("*", "a wildcard field mask pays for fields nothing reads");
    }

    /// <summary>
    /// Key hygiene, at the wire. A URI reaches access logs, proxy logs and
    /// <see cref="HttpRequestException"/> messages; a request body reaches none
    /// of those, but asserting on both costs nothing and closes the obvious
    /// "move it into the payload" regression too.
    /// </summary>
    [Fact]
    public async Task EstimateAsync_never_puts_the_api_key_in_the_request_uri_or_body()
    {
        var harness = Build(StubHttpMessageHandler.RespondingWithJson(Matrix(RouteExists(0, 4_100, "600s"))));

        await harness.Provider.EstimateAsync(Origin, [FirstDestination]);

        var request = harness.Handler.Requests[0];
        request.RequestUri!.ToString().Should().NotContain(ApiKey);
        request.RequestUri.Query.Should().BeEmpty("a query string is the one part of a request that ends up in every access log");
        request.Body.Should().NotContain(ApiKey);
    }

    // ---------------------------------------------------------------------
    // Key hygiene beyond the request
    // ---------------------------------------------------------------------

    /// <summary>
    /// The credential must survive none of the failure paths into the log
    /// stream - and a failure path is exactly where a careless diagnostic
    /// ("here is everything I was configured with") tends to get added.
    /// </summary>
    [Theory]
    [InlineData("http-500")]
    [InlineData("http-403")]
    [InlineData("http-429")]
    [InlineData("transport")]
    [InlineData("timeout")]
    [InlineData("malformed")]
    [InlineData("unexpected")]
    public async Task EstimateAsync_never_writes_the_api_key_to_the_log_whatever_fails(string failure)
    {
        var harness = Build(HandlerFor(failure));

        await harness.Provider.EstimateAsync(Origin, ThreeDestinations);

        harness.Logger.Entries.Should().NotBeEmpty("a degraded call that logs nothing is an outage nobody can see");
        harness.Logger.Text.Should().NotContain(ApiKey);
    }

    /// <summary>
    /// A cache key travels to Redis, shows up in <c>redis-cli</c> output and in
    /// this application's own cache logs. It is derived from coordinates only.
    /// </summary>
    [Fact]
    public async Task EstimateAsync_never_puts_the_api_key_in_a_cache_key()
    {
        var harness = Build(StubHttpMessageHandler.RespondingWithJson(Matrix(
            RouteExists(0, 4_100, "600s"),
            RouteExists(1, 6_200, "900s"))));

        await harness.Provider.EstimateAsync(Origin, [FirstDestination, SecondDestination]);

        harness.Cache.Keys.Should().BeEquivalentTo(
        [
            CacheKeys.RouteEstimate(Origin.Latitude, Origin.Longitude, FirstDestination.Latitude, FirstDestination.Longitude),
            CacheKeys.RouteEstimate(Origin.Latitude, Origin.Longitude, SecondDestination.Latitude, SecondDestination.Longitude)
        ]);
        harness.Cache.Keys.Should().OnlyContain(key => !key.Contains(ApiKey));
    }

    /// <summary>
    /// The one exception this class is allowed to let out is the caller's own
    /// cancellation - so it is also the one exception that could carry the
    /// credential out with it. It must not.
    /// </summary>
    [Fact]
    public async Task EstimateAsync_propagates_caller_cancellation_without_carrying_the_api_key()
    {
        var harness = Build(StubHttpMessageHandler.RespondingWithJson(Matrix(RouteExists(0, 4_100, "600s"))));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var act = async () => await harness.Provider.EstimateAsync(Origin, ThreeDestinations, cancellation.Token);

        var thrown = await act.Should().ThrowAsync<OperationCanceledException>(
            "the caller's own request going away is cooperative cancellation, not a provider failure - swallowing it would break graceful shutdown");
        thrown.Which.ToString().Should().NotContain(ApiKey);
        harness.Logger.Text.Should().NotContain(ApiKey);
    }

    /// <summary>
    /// Structural pin for the reason <see cref="GoogleMapsOptions"/> is a class
    /// and not a record: a record's synthesized <c>ToString</c> prints every
    /// property, so one <c>_logger.LogDebug("{Options}", options)</c> anywhere
    /// would put the credential in the log stream. A reviewer "tidying" this
    /// into a record reintroduces that silently, and no behavioural test would
    /// notice - which is why this asserts on the type's shape.
    /// </summary>
    [Fact]
    public void GoogleMapsOptions_is_not_a_record_so_no_synthesized_ToString_can_print_the_key()
    {
        var optionsType = typeof(GoogleMapsOptions);

        const BindingFlags AnyInstanceMember = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        // The two artefacts the compiler only emits for a record.
        optionsType.GetMethod("<Clone>$", AnyInstanceMember)
            .Should().BeNull("a <Clone>$ method means this became a record, and a record prints its properties in ToString");
        optionsType.GetProperty("EqualityContract", AnyInstanceMember)
            .Should().BeNull("an EqualityContract property means this became a record");

        new GoogleMapsOptions { ApiKey = ApiKey }.ToString()
            .Should().Be(optionsType.ToString(), "the default object.ToString prints the type name and nothing else");
    }

    // ---------------------------------------------------------------------
    // Parsing the per-element result
    // ---------------------------------------------------------------------

    /// <summary>
    /// <c>computeRouteMatrix</c> streams elements in whatever order they
    /// complete, so the response order says nothing about the request order.
    /// Every element must be routed back by its own <c>destinationIndex</c>.
    /// </summary>
    [Fact]
    public async Task EstimateAsync_maps_each_element_back_to_its_own_destination_whatever_order_they_arrive_in()
    {
        var harness = Build(StubHttpMessageHandler.RespondingWithJson(Matrix(
            RouteExists(2, 8_300, "1200s"),
            RouteExists(0, 4_100, "600s"),
            RouteExists(1, 6_200, "900s"))));

        var estimates = await harness.Provider.EstimateAsync(Origin, ThreeDestinations);

        estimates.Select(e => e.DestinationIndex).Should().Equal([0, 1, 2]);
        estimates.Select(e => e.DistanceMetres).Should().Equal([4_100, 6_200, 8_300]);
        estimates.Select(e => e.DurationSeconds).Should().Equal([600, 900, 1_200]);
        estimates.Should().OnlyContain(e => e.Source == RouteEstimateSource.GoogleMaps);
    }

    /// <summary>
    /// proto3 drops default values on the wire, so the element for destination
    /// zero legitimately arrives with no <c>destinationIndex</c> field at all.
    /// Reading that as "some other destination" would misattribute a real ETA.
    /// </summary>
    [Fact]
    public async Task EstimateAsync_treats_an_omitted_destination_index_as_the_first_destination()
    {
        var harness = Build(StubHttpMessageHandler.RespondingWithJson(
            """[{"distanceMeters":4100,"duration":"600s","condition":"ROUTE_EXISTS"}]"""));

        var estimates = await harness.Provider.EstimateAsync(Origin, [FirstDestination]);

        estimates.Should().ContainSingle();
        estimates[0].Should().Be(new RouteEstimate(0, 4_100, 600, RouteEstimateSource.GoogleMaps));
    }

    /// <summary>Same proto3 rule for a zero-length leg: absent <c>distanceMeters</c> means zero, not "unknown".</summary>
    [Fact]
    public async Task EstimateAsync_treats_an_omitted_distance_as_a_zero_length_leg()
    {
        var harness = Build(StubHttpMessageHandler.RespondingWithJson(
            """[{"destinationIndex":0,"duration":"0s","condition":"ROUTE_EXISTS"}]"""));

        var estimates = await harness.Provider.EstimateAsync(Origin, [FirstDestination]);

        estimates[0].Should().Be(new RouteEstimate(0, 0, 0, RouteEstimateSource.GoogleMaps));
    }

    /// <summary>An element whose <c>status</c> is an empty object is a success - code 0 is the proto3 default and is dropped.</summary>
    [Fact]
    public async Task EstimateAsync_treats_an_empty_status_object_as_success()
    {
        var harness = Build(StubHttpMessageHandler.RespondingWithJson(
            """[{"destinationIndex":0,"distanceMeters":4100,"duration":"600s","condition":"ROUTE_EXISTS","status":{}}]"""));

        var estimates = await harness.Provider.EstimateAsync(Origin, [FirstDestination]);

        estimates[0].Source.Should().Be(RouteEstimateSource.GoogleMaps);
    }

    /// <summary>A protobuf Duration is a decimal string of seconds; whole seconds is all this application stores.</summary>
    [Theory]
    [InlineData("600s", 600)]
    [InlineData("123.5s", 124)]
    [InlineData("123.4s", 123)]
    [InlineData("0.5s", 1)]
    [InlineData("0s", 0)]
    public async Task EstimateAsync_rounds_a_protobuf_duration_to_whole_seconds(string duration, int expectedSeconds)
    {
        var harness = Build(StubHttpMessageHandler.RespondingWithJson(Matrix(RouteExists(0, 4_100, duration))));

        var estimates = await harness.Provider.EstimateAsync(Origin, [FirstDestination]);

        estimates[0].DurationSeconds.Should().Be(expectedSeconds);
        estimates[0].Source.Should().Be(RouteEstimateSource.GoogleMaps);
    }

    /// <summary>
    /// The per-destination failure statuses this API really produces. There is
    /// no ZERO_RESULTS/NOT_FOUND string on the Routes API - an unroutable pair
    /// comes back as a non-ROUTE_EXISTS condition or a non-zero
    /// <c>google.rpc.Status</c> code - and each must degrade that one
    /// destination without touching the others.
    /// </summary>
    [Theory]
    // The documented "no route between these two points" condition.
    [InlineData("""{"destinationIndex":0,"condition":"ROUTE_NOT_FOUND"}""")]
    // A condition this build has never heard of must not be read as success.
    [InlineData("""{"destinationIndex":0,"distanceMeters":4100,"duration":"600s","condition":"SOME_FUTURE_CONDITION"}""")]
    // No condition at all.
    [InlineData("""{"destinationIndex":0,"distanceMeters":4100,"duration":"600s"}""")]
    // INVALID_ARGUMENT on this element only.
    [InlineData("""{"destinationIndex":0,"condition":"ROUTE_EXISTS","status":{"code":3,"message":"invalid waypoint"}}""")]
    // A route that claims to exist but carries no readable duration.
    [InlineData("""{"destinationIndex":0,"distanceMeters":4100,"condition":"ROUTE_EXISTS"}""")]
    [InlineData("""{"destinationIndex":0,"distanceMeters":4100,"duration":"","condition":"ROUTE_EXISTS"}""")]
    [InlineData("""{"destinationIndex":0,"distanceMeters":4100,"duration":"600","condition":"ROUTE_EXISTS"}""")]
    [InlineData("""{"destinationIndex":0,"distanceMeters":4100,"duration":"about ten minutes","condition":"ROUTE_EXISTS"}""")]
    [InlineData("""{"destinationIndex":0,"distanceMeters":4100,"duration":"-60s","condition":"ROUTE_EXISTS"}""")]
    [InlineData("""{"destinationIndex":0,"distanceMeters":4100,"duration":"NaNs","condition":"ROUTE_EXISTS"}""")]
    [InlineData("""{"destinationIndex":0,"distanceMeters":4100,"duration":"99999999999s","condition":"ROUTE_EXISTS"}""")]
    public async Task EstimateAsync_degrades_only_the_destination_whose_element_it_cannot_use(string unusableElement)
    {
        var harness = Build(StubHttpMessageHandler.RespondingWithJson(Matrix(
            unusableElement,
            RouteExists(1, 6_200, "900s"))));

        var estimates = await harness.Provider.EstimateAsync(Origin, [FirstDestination, SecondDestination]);

        estimates.Should().HaveCount(2);
        estimates[0].Should().Be(SandboxEstimateFor(FirstDestination, 0));
        estimates[1].Should().Be(new RouteEstimate(1, 6_200, 900, RouteEstimateSource.GoogleMaps),
            "one unroutable pair is not a reason to throw away the road data for its neighbours");
    }

    /// <summary>
    /// The coverage gap task 291 was filed for, found while verifying task
    /// 286: deleting <c>TryReadLeg</c>'s <c>element.Status?.Code is not (null
    /// or 0)</c> guard broke no test at all.
    ///
    /// Every element-status case above omits the <c>duration</c>, so the
    /// duration parse refuses those elements one step later and the status
    /// guard is never the reason they degrade. The only shape that actually
    /// reaches the guard is an element that is fully formed in every OTHER
    /// respect - <c>ROUTE_EXISTS</c>, a readable duration, a distance - and
    /// still carries a failed per-element <c>google.rpc.Status</c>. That is a
    /// real Routes API response: the request as a whole succeeded, and this
    /// one origin-destination pair did not.
    ///
    /// Without the guard the numbers on such an element are read as a
    /// measured route, and a booking is then ranked, ETA'd and travel-checked
    /// against a distance Google explicitly refused to stand behind.
    /// </summary>
    [Theory]
    // google.rpc.Code values a failed element realistically carries.
    [InlineData(3)]   // INVALID_ARGUMENT
    [InlineData(4)]   // DEADLINE_EXCEEDED
    [InlineData(8)]   // RESOURCE_EXHAUSTED
    [InlineData(14)]  // UNAVAILABLE
    // Not a google.rpc.Code this build knows. Anything that is not 0 is not OK.
    [InlineData(9999)]
    [InlineData(-1)]
    public async Task EstimateAsync_refuses_a_fully_formed_element_that_carries_a_failed_status(int statusCode)
    {
        var cache = new InMemoryCacheService();
        var harness = Build(
            StubHttpMessageHandler.RespondingWithJson(Matrix(
                """{"originIndex":0,"destinationIndex":0,"distanceMeters":4100,"duration":"600s","condition":"ROUTE_EXISTS","status":{"code":CODE,"message":"element failed"}}"""
                    .Replace("CODE", statusCode.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal),
                RouteExists(1, 6_200, "900s"))),
            cache: cache);

        var estimates = await harness.Provider.EstimateAsync(Origin, [FirstDestination, SecondDestination]);

        estimates.Should().HaveCount(2);
        estimates[0].Should().Be(
            SandboxEstimateFor(FirstDestination, 0),
            "a failed google.rpc.Status is Google saying it could not route this pair - the numbers beside it are not a measurement");
        estimates[0].Source.Should().Be(RouteEstimateSource.Sandbox);
        estimates[0].DurationSeconds.Should().NotBe(600, "600s is the refused element's own claim, not an answer");

        estimates[1].Should().Be(
            new RouteEstimate(1, 6_200, 900, RouteEstimateSource.GoogleMaps),
            "one failed pair is not a reason to throw away the road data for its neighbours");

        cache.Keys.Should().HaveCount(1, "only the leg Google actually stood behind may be cached - a refused pair must not be served from the cache for the whole TTL");
    }

    /// <summary>
    /// The same defect at its most expensive. A failed element carrying
    /// <c>"duration":"0s"</c> is not merely a wrong ETA: task 289's travel
    /// check reads a zero-second leg as "no drive, so no handover buffer
    /// either" and lets the candidate through, so an unguarded status failure
    /// turns into a provider booked back-to-back across the city.
    /// </summary>
    [Fact]
    public async Task EstimateAsync_does_not_read_a_failed_element_as_a_zero_length_leg()
    {
        var harness = Build(StubHttpMessageHandler.RespondingWithJson(
            """[{"originIndex":0,"destinationIndex":0,"duration":"0s","condition":"ROUTE_EXISTS","status":{"code":3,"message":"invalid waypoint"}}]"""));

        var estimates = await harness.Provider.EstimateAsync(Origin, [FirstDestination]);

        estimates[0].Should().Be(
            SandboxEstimateFor(FirstDestination, 0),
            "'no route here' must not arrive downstream wearing the one shape that means 'no drive needed'");
        estimates[0].DurationSeconds.Should().BeGreaterThan(0, "these two points are kilometres apart by any measure");
    }

    /// <summary>
    /// An element-level failure must not put Google's own message into this
    /// application's logs: the response body is unvalidated third-party text
    /// and is deliberately not echoed.
    /// </summary>
    [Fact]
    public async Task EstimateAsync_logs_an_element_failure_without_echoing_the_response_body()
    {
        const string GoogleMessage = "invalid waypoint: 12.94,77.61";
        string responseBody =
            """[{"destinationIndex":0,"condition":"ROUTE_EXISTS","status":{"code":3,"message":"MESSAGE"}}]"""
                .Replace("MESSAGE", GoogleMessage, StringComparison.Ordinal);

        var harness = Build(StubHttpMessageHandler.RespondingWithJson(responseBody));

        await harness.Provider.EstimateAsync(Origin, [FirstDestination]);

        harness.Logger.Entries.Should().NotBeEmpty();
        harness.Logger.Text.Should().NotContain(GoogleMessage);
        harness.Logger.Text.Should().Contain(
            "status 3",
            "the status code is diagnostic; the body text is not this application's to repeat");
    }

    /// <summary>
    /// An element pointing outside the batch it belongs to is nonsense, and
    /// writing it into <c>estimates[thatIndex]</c> would attribute one
    /// candidate's ETA to another. It is dropped, loudly.
    /// </summary>
    [Fact]
    public async Task EstimateAsync_ignores_an_element_outside_the_requested_destination_range()
    {
        var harness = Build(StubHttpMessageHandler.RespondingWithJson(Matrix(
            RouteExists(0, 4_100, "600s"),
            RouteExists(7, 9_900, "1800s"))));

        var estimates = await harness.Provider.EstimateAsync(Origin, [FirstDestination, SecondDestination]);

        estimates.Should().HaveCount(2);
        estimates[0].Source.Should().Be(RouteEstimateSource.GoogleMaps);
        estimates[1].Should().Be(SandboxEstimateFor(SecondDestination, 1));
        harness.Logger.At(LogLevel.Warning).Should().NotBeEmpty("an out-of-range element means the response is not what this code believes it is");
    }

    /// <summary>A destination Google simply did not answer for still owes the caller an estimate.</summary>
    [Fact]
    public async Task EstimateAsync_fills_in_a_destination_the_response_omitted_entirely()
    {
        var harness = Build(StubHttpMessageHandler.RespondingWithJson(Matrix(
            RouteExists(0, 4_100, "600s"),
            RouteExists(1, 6_200, "900s"))));

        var estimates = await harness.Provider.EstimateAsync(Origin, ThreeDestinations);

        estimates.Should().HaveCount(3);
        estimates[2].Should().Be(SandboxEstimateFor(ThirdDestination, 2));
    }

    // ---------------------------------------------------------------------
    // The operational failure matrix
    // ---------------------------------------------------------------------

    private static StubHttpMessageHandler HandlerFor(string failure) => failure switch
    {
        "http-500" => StubHttpMessageHandler.Responding(HttpStatusCode.InternalServerError, "upstream exploded"),
        "http-503" => StubHttpMessageHandler.Responding(HttpStatusCode.ServiceUnavailable),
        // The Routes API's form of OVER_QUERY_LIMIT: the legacy APIs signalled
        // quota exhaustion in a 200 body, Routes signals it as 429.
        "http-429" => StubHttpMessageHandler.Responding(HttpStatusCode.TooManyRequests),
        "http-403" => StubHttpMessageHandler.Responding(HttpStatusCode.Forbidden),
        "http-401" => StubHttpMessageHandler.Responding(HttpStatusCode.Unauthorized),
        "http-400" => StubHttpMessageHandler.Responding(HttpStatusCode.BadRequest),
        "transport" => StubHttpMessageHandler.Throwing(() => new HttpRequestException("No such host is known.")),
        // Exactly what HttpClient raises when its own timeout fires: a
        // cancellation the caller never asked for.
        "timeout" => StubHttpMessageHandler.Throwing(() =>
            new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout.", new TimeoutException())),
        "malformed" => StubHttpMessageHandler.RespondingWithJson("{ this is not the matrix you are looking for"),
        "empty-body" => StubHttpMessageHandler.RespondingWithJson(string.Empty),
        "json-null" => StubHttpMessageHandler.RespondingWithJson("null"),
        "wrong-shape" => StubHttpMessageHandler.RespondingWithJson("""{"error":{"code":403,"status":"PERMISSION_DENIED"}}"""),
        "unexpected" => StubHttpMessageHandler.Throwing(() => new InvalidOperationException("something nobody classified")),
        _ => throw new ArgumentOutOfRangeException(nameof(failure), failure, "Unknown failure mode.")
    };

    /// <summary>
    /// The contract, under every operational failure that matters: exactly one
    /// estimate per destination, index-aligned, sourced from the sandbox, and
    /// no exception. This is the single most important test in the file - a
    /// routing outage must degrade an ETA, never fail a booking.
    /// </summary>
    [Theory]
    [InlineData("http-500")]
    [InlineData("http-503")]
    [InlineData("http-429")]
    [InlineData("http-403")]
    [InlineData("http-401")]
    [InlineData("http-400")]
    [InlineData("transport")]
    [InlineData("timeout")]
    [InlineData("malformed")]
    [InlineData("empty-body")]
    [InlineData("json-null")]
    [InlineData("wrong-shape")]
    [InlineData("unexpected")]
    public async Task EstimateAsync_falls_back_to_the_sandbox_rather_than_throwing(string failure)
    {
        var harness = Build(HandlerFor(failure));

        var act = async () => await harness.Provider.EstimateAsync(Origin, ThreeDestinations);

        var estimates = (await act.Should().NotThrowAsync()).Which;
        estimates.Should().HaveCount(3);
        estimates.Select(e => e.DestinationIndex).Should().Equal([0, 1, 2]);
        estimates.Should().OnlyContain(e => e.Source == RouteEstimateSource.Sandbox);
        estimates.Should().Equal(
        [
            SandboxEstimateFor(FirstDestination, 0),
            SandboxEstimateFor(SecondDestination, 1),
            SandboxEstimateFor(ThirdDestination, 2)
        ]);
    }

    /// <summary>
    /// Quota exhaustion is expected under load and self-heals, so it is a
    /// warning; a rejected credential stays broken until a human fixes it, so
    /// it is an error. Grading them the same way would either page someone for
    /// normal traffic or bury a dead key in the noise.
    /// </summary>
    [Theory]
    [InlineData("http-429", LogLevel.Warning)]
    [InlineData("http-403", LogLevel.Error)]
    [InlineData("http-401", LogLevel.Error)]
    [InlineData("http-500", LogLevel.Error)]
    public async Task EstimateAsync_grades_an_unsuccessful_status_by_whether_it_needs_a_human(string failure, LogLevel expectedLevel)
    {
        var harness = Build(HandlerFor(failure));

        await harness.Provider.EstimateAsync(Origin, [FirstDestination]);

        harness.Logger.At(expectedLevel).Should().NotBeEmpty();
        harness.Logger.Entries.Should().OnlyContain(entry => entry.Level == expectedLevel);
    }

    /// <summary>
    /// The response body of a failed call is unvalidated third-party text and
    /// must not be echoed into this application's logs.
    /// </summary>
    [Fact]
    public async Task EstimateAsync_does_not_echo_a_failed_response_body_into_the_log()
    {
        const string UpstreamBody = "upstream exploded";
        var harness = Build(StubHttpMessageHandler.Responding(HttpStatusCode.InternalServerError, UpstreamBody));

        await harness.Provider.EstimateAsync(Origin, [FirstDestination]);

        harness.Logger.Text.Should().NotContain(UpstreamBody);
        harness.Logger.Text.Should().Contain("500");
    }

    /// <summary>
    /// No key means no call. Registration already picks the sandbox in this
    /// case, so reaching the Google provider unconfigured means configuration
    /// changed under a live process - it degrades, and says so, rather than
    /// issuing a request that is certain to be rejected.
    /// </summary>
    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("   ", true)]
    [InlineData(ApiKey, false)]
    public async Task EstimateAsync_falls_back_without_calling_google_when_it_is_not_configured(string? apiKey, bool enabled)
    {
        var harness = Build(
            StubHttpMessageHandler.RespondingWithJson(Matrix(RouteExists(0, 4_100, "600s"))),
            new GoogleMapsOptions { ApiKey = apiKey, Enabled = enabled });

        var estimates = await harness.Provider.EstimateAsync(Origin, ThreeDestinations);

        harness.Handler.Requests.Should().BeEmpty("an unconfigured or switched-off integration must not spend a round trip to be told so");
        estimates.Should().HaveCount(3);
        estimates.Should().OnlyContain(e => e.Source == RouteEstimateSource.Sandbox);
        harness.Logger.At(LogLevel.Warning).Should().NotBeEmpty();
    }

    /// <summary>A null destination list is a programming error, not an external failure, and stays an exception.</summary>
    [Fact]
    public async Task EstimateAsync_rejects_a_null_destination_list()
    {
        var harness = Build(StubHttpMessageHandler.RespondingWithJson(Matrix()));

        var act = async () => await harness.Provider.EstimateAsync(Origin, null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    /// <summary>Nothing to estimate is not a reason to call anyone.</summary>
    [Fact]
    public async Task EstimateAsync_issues_no_request_for_an_empty_destination_list()
    {
        var harness = Build(StubHttpMessageHandler.RespondingWithJson(Matrix()));

        var estimates = await harness.Provider.EstimateAsync(Origin, []);

        estimates.Should().BeEmpty();
        harness.Handler.Requests.Should().BeEmpty();
    }

    // ---------------------------------------------------------------------
    // Cache behaviour
    // ---------------------------------------------------------------------

    /// <summary>
    /// The repeated pings of one in-progress job must not each be a billed
    /// element. A hit restores the Google source, because only Google-sourced
    /// legs are ever written.
    /// </summary>
    [Fact]
    public async Task EstimateAsync_serves_a_repeated_request_from_the_cache_without_calling_google_again()
    {
        var harness = Build(StubHttpMessageHandler.RespondingWithJson(Matrix(RouteExists(0, 4_100, "600s"))));

        var first = await harness.Provider.EstimateAsync(Origin, [FirstDestination]);
        var second = await harness.Provider.EstimateAsync(Origin, [FirstDestination]);

        harness.Handler.Requests.Should().ContainSingle("the second call is a cache hit");
        second.Should().Equal(first);
        second[0].Source.Should().Be(RouteEstimateSource.GoogleMaps, "a cached leg is still a measured leg");
    }

    /// <summary>A partial hit must narrow the request, not skip it and not repeat it.</summary>
    [Fact]
    public async Task EstimateAsync_only_asks_google_about_the_destinations_that_missed_the_cache()
    {
        var cache = new InMemoryCacheService();
        var harness = Build(
            StubHttpMessageHandler.RespondingWithJson(Matrix(RouteExists(0, 4_100, "600s"))),
            cache: cache);

        await harness.Provider.EstimateAsync(Origin, [FirstDestination]);

        // Second call adds a destination the cache has never seen.
        var handler = StubHttpMessageHandler.RespondingWithJson(Matrix(RouteExists(0, 6_200, "900s")));
        var warmed = Build(handler, cache: cache);

        var estimates = await warmed.Provider.EstimateAsync(Origin, [FirstDestination, SecondDestination]);

        handler.Requests.Should().ContainSingle();
        var destinations = BodyOf(handler.Requests[0])["destinations"]!.AsArray();
        destinations.Should().ContainSingle("only the miss is worth paying for");
        LatLngAt(BodyOf(handler.Requests[0]), "destinations", 0)
            .Should().Be((SecondDestination.Latitude, SecondDestination.Longitude));

        estimates.Should().Equal(
        [
            new RouteEstimate(0, 4_100, 600, RouteEstimateSource.GoogleMaps),
            new RouteEstimate(1, 6_200, 900, RouteEstimateSource.GoogleMaps)
        ]);
    }

    /// <summary>
    /// A sandbox estimate is cheap to recompute, and caching it would leave an
    /// approximation in place for the whole TTL after the outage that produced
    /// it had passed.
    /// </summary>
    [Fact]
    public async Task EstimateAsync_does_not_cache_a_sandbox_fallback()
    {
        var cache = new InMemoryCacheService();
        var outage = Build(StubHttpMessageHandler.Responding(HttpStatusCode.InternalServerError), cache: cache);

        await outage.Provider.EstimateAsync(Origin, [FirstDestination]);

        cache.Keys.Should().BeEmpty("an approximation must not outlive the outage that produced it");

        var recovered = Build(
            StubHttpMessageHandler.RespondingWithJson(Matrix(RouteExists(0, 4_100, "600s"))),
            cache: cache);

        var estimates = await recovered.Provider.EstimateAsync(Origin, [FirstDestination]);

        estimates[0].Source.Should().Be(RouteEstimateSource.GoogleMaps, "the next call must be free to ask Google again");
    }

    /// <summary>Zero TTL disables the cache on both sides - reads and writes.</summary>
    [Fact]
    public async Task EstimateAsync_neither_reads_nor_writes_the_cache_when_the_ttl_is_zero()
    {
        var cache = new InMemoryCacheService();
        var harness = Build(
            StubHttpMessageHandler.RespondingWithJson(Matrix(RouteExists(0, 4_100, "600s"))),
            new GoogleMapsOptions { ApiKey = ApiKey, CacheTtlSeconds = 0 },
            cache);

        await harness.Provider.EstimateAsync(Origin, [FirstDestination]);
        await harness.Provider.EstimateAsync(Origin, [FirstDestination]);

        cache.Keys.Should().BeEmpty();
        harness.Handler.Requests.Should().HaveCount(2, "with caching off every call is a fresh measurement");
    }

    // ---------------------------------------------------------------------
    // Batching and the fan-out cap
    // ---------------------------------------------------------------------

    /// <summary>Five points strung out along one meridian, so every one of them is a distinct cache key.</summary>
    private static List<GeoCoordinate> DestinationsSpread(int count) =>
        [.. Enumerable.Range(0, count).Select(offset => new GeoCoordinate(12.9400m + (offset * 0.0100m), 77.6100m))];

    /// <summary>
    /// Answers whatever it is asked about, one <c>ROUTE_EXISTS</c> element per
    /// destination in the batch. Element indexes are batch-relative, which is
    /// how the real API numbers them and is exactly the detail that goes wrong
    /// if chunking ever stops translating them back.
    /// </summary>
    private static StubHttpMessageHandler EchoingHandler() =>
        new(request =>
        {
            int count = BodyOf(request)["destinations"]!.AsArray().Count;
            string body = Matrix([.. Enumerable.Range(0, count).Select(index => RouteExists(index, 4_100, "600s"))]);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) };
        });

    /// <summary>
    /// Destinations past the per-call size become further requests, not one
    /// over-sized request Google would reject - and each chunk's batch-relative
    /// element indexes are translated back to the caller's positions.
    /// </summary>
    [Fact]
    public async Task EstimateAsync_chunks_destinations_into_separate_requests_at_the_configured_call_size()
    {
        var harness = Build(EchoingHandler(), new GoogleMapsOptions { ApiKey = ApiKey, MaxDestinationsPerCall = 2 });

        var estimates = await harness.Provider.EstimateAsync(Origin, DestinationsSpread(5));

        harness.Handler.Requests
            .Select(request => BodyOf(request)["destinations"]!.AsArray().Count)
            .Should().Equal([2, 2, 1]);
        estimates.Select(e => e.DestinationIndex).Should().Equal([0, 1, 2, 3, 4]);
        estimates.Should().OnlyContain(e => e.Source == RouteEstimateSource.GoogleMaps);
    }

    /// <summary>A candidate set that fits the per-call size stays one round trip.</summary>
    [Fact]
    public async Task EstimateAsync_keeps_a_set_within_the_call_size_in_a_single_request()
    {
        var harness = Build(EchoingHandler());

        var estimates = await harness.Provider.EstimateAsync(Origin, DestinationsSpread(5));

        harness.Handler.Requests.Should().ContainSingle("five destinations fit inside the default call size of 25");
        estimates.Should().HaveCount(5);
    }

    /// <summary>
    /// The cost circuit breaker. A caller that passes an unfiltered candidate
    /// list must not turn one booking into hundreds of billed elements - and
    /// the destinations past the cap are still answered, from the sandbox, with
    /// the overflow logged so the offending caller is visible.
    /// </summary>
    [Fact]
    public async Task EstimateAsync_stops_billing_at_the_fan_out_cap_and_answers_the_rest_from_the_sandbox()
    {
        var harness = Build(EchoingHandler(), new GoogleMapsOptions { ApiKey = ApiKey, MaxDestinationsPerEstimate = 3 });

        var estimates = await harness.Provider.EstimateAsync(Origin, DestinationsSpread(5));

        harness.Handler.Requests.Should().ContainSingle();
        BodyOf(harness.Handler.Requests[0])["destinations"]!.AsArray().Should().HaveCount(3, "the cap is on billed elements, not on the answer");

        estimates.Should().HaveCount(5, "an over-cap request is trimmed, never rejected - the caller is still owed one estimate per destination");
        estimates.Take(3).Should().OnlyContain(e => e.Source == RouteEstimateSource.GoogleMaps);
        estimates.Skip(3).Should().OnlyContain(e => e.Source == RouteEstimateSource.Sandbox);
        estimates.Select(e => e.DestinationIndex).Should().Equal([0, 1, 2, 3, 4]);
        harness.Logger.At(LogLevel.Warning).Should().NotBeEmpty("an over-cap caller is a cost bug worth seeing in the logs");
    }

    /// <summary>
    /// One failing chunk must not take the successful ones with it: the
    /// fallback is per destination, not per request.
    /// </summary>
    [Fact]
    public async Task EstimateAsync_keeps_the_answers_from_the_chunks_that_succeeded()
    {
        int call = 0;
        var harness = Build(
            new StubHttpMessageHandler(request =>
            {
                if (call++ == 1)
                {
                    return new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent(string.Empty) };
                }

                int count = BodyOf(request)["destinations"]!.AsArray().Count;
                string body = Matrix([.. Enumerable.Range(0, count).Select(index => RouteExists(index, 4_100, "600s"))]);
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) };
            }),
            new GoogleMapsOptions { ApiKey = ApiKey, MaxDestinationsPerCall = 2 });

        var estimates = await harness.Provider.EstimateAsync(Origin, DestinationsSpread(4));

        estimates.Should().HaveCount(4);
        estimates.Take(2).Should().OnlyContain(e => e.Source == RouteEstimateSource.GoogleMaps);
        estimates.Skip(2).Should().OnlyContain(e => e.Source == RouteEstimateSource.Sandbox);
        estimates.Select(e => e.DestinationIndex).Should().Equal([0, 1, 2, 3]);
    }
}

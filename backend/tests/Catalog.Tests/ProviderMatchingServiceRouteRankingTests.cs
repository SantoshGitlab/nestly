using FluentAssertions;
using Microsoft.Extensions.Options;
using Nestly.Application;
using Nestly.Application.Bookings;
using Nestly.Application.Routing;
using Nestly.Domain;
using Nestly.Infrastructure.Options;
using Nestly.Infrastructure.Persistence;
using Nestly.Infrastructure.Persistence.Repositories;
using Nestly.Infrastructure.Services;

namespace Nestly.Catalog.Tests;

/// <summary>
/// Covers task 267: ranking auto-assignment candidates by real road travel
/// time rather than great-circle distance, and the cost caps and fallbacks
/// around it.
///
/// The defect being pinned: straight-line distance picks the wrong provider
/// whenever a river, a highway or a one-way system puts the nearest-by-air
/// candidate furthest by road. Every test here drives a stubbed
/// <see cref="IRouteEstimateProvider"/> - never real HTTP - so the road
/// network can be made to disagree with the map on purpose.
/// </summary>
public sealed class ProviderMatchingServiceRouteRankingTests : IClassFixture<TestDatabase>
{
    private readonly TestDatabase _db;

    public ProviderMatchingServiceRouteRankingTests(TestDatabase db) => _db = db;

    // Booking address: Koramangala, Bengaluru. Each provider below sits due
    // west of it on the same latitude, so straight-line distance is a simple
    // function of longitude and the intended air ordering is obvious by
    // inspection.
    private const decimal BookingLatitude = 12.9352m;
    private const decimal BookingLongitude = 77.6245m;

    private const decimal OneKilometreWest = 77.6145m;    // ~1.1 km
    private const decimal TwoKilometresWest = 77.6045m;   // ~2.2 km
    private const decimal ThreeKilometresWest = 77.5945m; // ~3.3 km
    private const decimal FourKilometresWest = 77.5845m;  // ~4.3 km
    private const decimal FiveKilometresWest = 77.5745m;  // ~5.4 km

    // Chennai - far outside any sane route-ranking radius.
    private const decimal DistantLatitude = 13.0827m;
    private const decimal DistantLongitude = 80.2707m;

    private const int SlowRoadSeconds = 1_800;
    private const int FastRoadSeconds = 420;

    /// <summary>One candidate's road leg, as the stub should answer for it.</summary>
    private sealed record RoadLeg(int DurationSeconds, RouteEstimateSource Source = RouteEstimateSource.GoogleMaps);

    /// <summary>
    /// A stubbed router that answers from a table keyed by destination
    /// coordinate and records every call, so tests can assert how many
    /// requests were issued and what each carried - which is the only way to
    /// see the pre-filter doing its job.
    /// </summary>
    private sealed class RecordingRouteEstimateProvider : IRouteEstimateProvider
    {
        private readonly IReadOnlyDictionary<GeoCoordinate, RoadLeg> _legs;

        public RecordingRouteEstimateProvider(IReadOnlyDictionary<GeoCoordinate, RoadLeg> legs) => _legs = legs;

        public List<(GeoCoordinate Origin, IReadOnlyList<GeoCoordinate> Destinations)> Calls { get; } = [];

        public Task<IReadOnlyList<RouteEstimate>> EstimateAsync(
            GeoCoordinate origin,
            IReadOnlyList<GeoCoordinate> destinations,
            CancellationToken cancellationToken = default)
        {
            Calls.Add((origin, destinations));

            IReadOnlyList<RouteEstimate> estimates = destinations
                .Select((destination, index) =>
                {
                    if (!_legs.TryGetValue(destination, out var leg))
                    {
                        throw new InvalidOperationException($"No road leg configured for destination {destination}.");
                    }

                    // Road metres are unused by ranking; a plausible ~29 km/h
                    // figure keeps the stub's responses internally consistent.
                    return new RouteEstimate(index, leg.DurationSeconds * 8, leg.DurationSeconds, leg.Source);
                })
                .ToList();

            return Task.FromResult(estimates);
        }
    }

    private sealed record Fixture(Customer Customer, City City, Pincode Pincode, Category Category, Service Service, SlotWindow Window);

    private static Fixture Seed(NestlyDbContext context)
    {
        var pincodeCode = Guid.NewGuid().ToString("N")[..6];
        var customer = new Customer(Guid.NewGuid(), "9" + Guid.NewGuid().ToString("N")[..9], "Asha Rao", CustomerStatus.Active);
        var state = new State(Guid.NewGuid(), "Karnataka", "KA" + Guid.NewGuid().ToString("N")[..6]);
        var city = new City(Guid.NewGuid(), state.Id, "Bengaluru");
        var pincode = new Pincode(Guid.NewGuid(), city.Id, pincodeCode);
        var category = new Category(Guid.NewGuid(), "Cleaning", "cleaning-" + Guid.NewGuid(), "desc");
        var service = new Service(Guid.NewGuid(), category.Id, "Deep Clean", "deep-clean-" + Guid.NewGuid(), "desc", 500m);
        var window = new SlotWindow(Guid.NewGuid(), city.Id, "Morning", TimeSpan.FromHours(9), TimeSpan.FromHours(13));

        context.Add(customer);
        context.States.Add(state);
        context.Cities.Add(city);
        context.Pincodes.Add(pincode);
        context.Add(category);
        context.Add(service);
        context.SlotWindows.Add(window);
        context.SaveChanges();

        return new Fixture(customer, city, pincode, category, service, window);
    }

    private static Guid AddBooking(NestlyDbContext context, Fixture f)
    {
        var address = new AddressSnapshot(
            "Home", "221B Baker Street", null, null, f.Pincode.Code, f.City.Name, "Karnataka",
            BookingLatitude, BookingLongitude, "Asha Rao", "9876543210");
        var slot = new SlotSnapshot(f.Window.Id, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)), "Morning", TimeSpan.FromHours(9), TimeSpan.FromHours(13));
        var price = new PriceSnapshot(500m, 1, 500m, 0m, 50m, 550m, 18m, 99m, 10m, 659m);
        var booking = new Booking(Guid.NewGuid(), f.Customer.Id, new CustomerSnapshot(f.Customer.Name, f.Customer.Mobile), null, address, slot, price);
        booking.AddItem(Guid.NewGuid(), f.Service.Id, f.Service.Name, f.Service.Slug, 500m, 1);
        context.Add(booking);
        return booking.Id;
    }

    private static Provider AddEligibleProvider(NestlyDbContext context, Fixture f, decimal? latitude, decimal? longitude)
    {
        var provider = new Provider(Guid.NewGuid(), "Ravi Kumar", "Ravi's Repairs", ProviderType.Individual, "+9198" + Guid.NewGuid().ToString("N")[..8]);
        provider.ChangeStatus(ProviderStatus.Active);
        provider.UpdateLocation(latitude, longitude);
        context.Add(provider);
        context.Add(new ProviderSkillMapping(Guid.NewGuid(), provider.Id, f.Category.Id));
        context.Add(new ProviderServiceArea(Guid.NewGuid(), provider.Id, f.City.Id));
        return provider;
    }

    private static GeoCoordinate At(Provider provider) => new(provider.Latitude!.Value, provider.Longitude!.Value);

    private static ProviderMatchingService BuildService(
        NestlyDbContext context,
        IRouteEstimateProvider routeEstimateProvider,
        AutoAssignmentOptions? options = null) =>
        new(new BookingRepository(context), context, routeEstimateProvider, Options.Create(options ?? new AutoAssignmentOptions()));

    /// <summary>
    /// The defect this task exists to fix: the provider a kilometre away
    /// across an unbridged river takes half an hour to arrive, while the one
    /// five kilometres away on a clear arterial road takes seven minutes.
    /// Straight-line ranking sends the wrong one - and does, when the kill
    /// switch is off (see the next test, which asserts exactly the opposite
    /// order on the same seed data).
    /// </summary>
    [Fact]
    public async Task FindCandidatesAsync_ranks_by_road_travel_time_not_straight_line_distance()
    {
        Fixture f;
        Provider nearestByAir, fastestByRoad;
        Guid bookingId;
        using (var context = _db.CreateContext())
        {
            f = Seed(context);
            nearestByAir = AddEligibleProvider(context, f, BookingLatitude, OneKilometreWest);
            fastestByRoad = AddEligibleProvider(context, f, BookingLatitude, FiveKilometresWest);
            bookingId = AddBooking(context, f);
            context.SaveChanges();
        }

        var routes = new RecordingRouteEstimateProvider(new Dictionary<GeoCoordinate, RoadLeg>
        {
            [At(nearestByAir)] = new(SlowRoadSeconds),
            [At(fastestByRoad)] = new(FastRoadSeconds)
        });

        using var readContext = _db.CreateContext();
        var candidates = await BuildService(readContext, routes).FindCandidatesAsync(bookingId);

        candidates.Select(c => c.ProviderId).Should().Equal(
            [fastestByRoad.Id, nearestByAir.Id],
            "the road is what the provider has to drive, and the nearest-by-air candidate is half an hour away on it");
        candidates[0].TravelDurationSeconds.Should().Be(FastRoadSeconds);
        candidates[1].TravelDurationSeconds.Should().Be(SlowRoadSeconds);

        // DistanceKm is untouched by task 267: still straight-line kilometres,
        // so it is now the *smaller* number on the *second* candidate.
        candidates[0].DistanceKm.Should().BeGreaterThan(candidates[1].DistanceKm!.Value);

        // The direction approximation, made visible: one call, from the
        // booking's address outwards to the candidates, not from each
        // candidate inwards.
        routes.Calls.Should().ContainSingle();
        routes.Calls[0].Origin.Should().Be(new GeoCoordinate(BookingLatitude, BookingLongitude));
    }

    /// <summary>
    /// The kill switch: with route ranking off, the service must behave
    /// exactly as it did before task 267 - straight-line order, no travel
    /// time, and no route call issued at all. Same seed data as the test
    /// above, asserting the opposite order, which is what makes that test a
    /// real regression guard rather than a restatement of the ordering code.
    /// </summary>
    [Fact]
    public async Task FindCandidatesAsync_falls_back_to_straight_line_order_when_route_ranking_is_disabled()
    {
        Fixture f;
        Provider nearestByAir, fastestByRoad;
        Guid bookingId;
        using (var context = _db.CreateContext())
        {
            f = Seed(context);
            nearestByAir = AddEligibleProvider(context, f, BookingLatitude, OneKilometreWest);
            fastestByRoad = AddEligibleProvider(context, f, BookingLatitude, FiveKilometresWest);
            bookingId = AddBooking(context, f);
            context.SaveChanges();
        }

        var routes = new RecordingRouteEstimateProvider(new Dictionary<GeoCoordinate, RoadLeg>
        {
            [At(nearestByAir)] = new(SlowRoadSeconds),
            [At(fastestByRoad)] = new(FastRoadSeconds)
        });

        using var readContext = _db.CreateContext();
        var candidates = await BuildService(readContext, routes, new AutoAssignmentOptions { RouteRankingEnabled = false })
            .FindCandidatesAsync(bookingId);

        candidates.Select(c => c.ProviderId).Should().Equal([nearestByAir.Id, fastestByRoad.Id]);
        candidates.Should().OnlyContain(c => c.TravelDurationSeconds == null);
        routes.Calls.Should().BeEmpty("a disabled feature must not spend money");
    }

    /// <summary>
    /// Task 250's determinism guarantee, moved onto the new ranking key: two
    /// candidates the road agrees exactly about must still come back in the
    /// same order every run.
    /// </summary>
    [Fact]
    public async Task FindCandidatesAsync_breaks_an_exact_travel_time_tie_deterministically_by_provider_id()
    {
        Fixture f;
        Provider nearer, farther;
        Guid bookingId;
        using (var context = _db.CreateContext())
        {
            f = Seed(context);
            nearer = AddEligibleProvider(context, f, BookingLatitude, OneKilometreWest);
            farther = AddEligibleProvider(context, f, BookingLatitude, FiveKilometresWest);
            bookingId = AddBooking(context, f);
            context.SaveChanges();
        }

        const int IdenticalSeconds = 900;
        var routes = new RecordingRouteEstimateProvider(new Dictionary<GeoCoordinate, RoadLeg>
        {
            [At(nearer)] = new(IdenticalSeconds),
            [At(farther)] = new(IdenticalSeconds)
        });

        var expectedOrder = new[] { nearer.Id, farther.Id }.OrderBy(id => id).ToList();

        using var readContext = _db.CreateContext();
        var candidates = await BuildService(readContext, routes).FindCandidatesAsync(bookingId);

        candidates.Should().OnlyContain(c => c.TravelDurationSeconds == IdenticalSeconds);
        candidates.Select(c => c.ProviderId).Should().Equal(
            expectedOrder,
            "an exact tie falls back to Provider.Id - deterministic, and carrying no ranking meaning (PROVIDER.md OPEN DECISIONS - AUTOMATIC ASSIGNMENT #3)");
    }

    /// <summary>A provider with no coordinates cannot be routed to, and keeps sorting last rather than being excluded - unchanged by task 267.</summary>
    [Fact]
    public async Task FindCandidatesAsync_still_sorts_an_unlocated_provider_last_when_ranking_by_travel_time()
    {
        Fixture f;
        Provider nearestByAir, fastestByRoad, unlocated;
        Guid bookingId;
        using (var context = _db.CreateContext())
        {
            f = Seed(context);
            nearestByAir = AddEligibleProvider(context, f, BookingLatitude, OneKilometreWest);
            fastestByRoad = AddEligibleProvider(context, f, BookingLatitude, FiveKilometresWest);
            unlocated = AddEligibleProvider(context, f, null, null);
            bookingId = AddBooking(context, f);
            context.SaveChanges();
        }

        var routes = new RecordingRouteEstimateProvider(new Dictionary<GeoCoordinate, RoadLeg>
        {
            [At(nearestByAir)] = new(SlowRoadSeconds),
            [At(fastestByRoad)] = new(FastRoadSeconds)
        });

        using var readContext = _db.CreateContext();
        var candidates = await BuildService(readContext, routes).FindCandidatesAsync(bookingId);

        candidates.Select(c => c.ProviderId).Should().Equal([fastestByRoad.Id, nearestByAir.Id, unlocated.Id]);
        candidates[2].DistanceKm.Should().BeNull();
        candidates[2].TravelDurationSeconds.Should().BeNull("there is nowhere to route to, so nothing was measured");
        routes.Calls[0].Destinations.Should().HaveCount(2, "an unlocated provider is not a destination");
    }

    /// <summary>
    /// The cost cap: a large candidate set must not fan out into a large
    /// billed request. Only the straight-line-nearest
    /// <see cref="AutoAssignmentOptions.MaxRouteCandidates"/> are priced, in
    /// one call; the rest keep their straight-line order behind them and stay
    /// fully assignable.
    /// </summary>
    [Fact]
    public async Task FindCandidatesAsync_prices_at_most_MaxRouteCandidates_destinations_in_one_call()
    {
        Fixture f;
        Provider first, second, third, fourth, fifth;
        Guid bookingId;
        using (var context = _db.CreateContext())
        {
            f = Seed(context);
            first = AddEligibleProvider(context, f, BookingLatitude, OneKilometreWest);
            second = AddEligibleProvider(context, f, BookingLatitude, TwoKilometresWest);
            third = AddEligibleProvider(context, f, BookingLatitude, ThreeKilometresWest);
            fourth = AddEligibleProvider(context, f, BookingLatitude, FourKilometresWest);
            fifth = AddEligibleProvider(context, f, BookingLatitude, FiveKilometresWest);
            bookingId = AddBooking(context, f);
            context.SaveChanges();
        }

        var routes = new RecordingRouteEstimateProvider(new Dictionary<GeoCoordinate, RoadLeg>
        {
            [At(first)] = new(SlowRoadSeconds),
            [At(second)] = new(FastRoadSeconds)
        });

        using var readContext = _db.CreateContext();
        var candidates = await BuildService(readContext, routes, new AutoAssignmentOptions { MaxRouteCandidates = 2 })
            .FindCandidatesAsync(bookingId);

        routes.Calls.Should().ContainSingle("one batched request per booking, never one per candidate");
        routes.Calls[0].Destinations.Should().Equal([At(first), At(second)], "the two nearest by air are the two worth measuring");

        candidates.Select(c => c.ProviderId).Should().Equal(
            [second.Id, first.Id, third.Id, fourth.Id, fifth.Id],
            "the priced pair is reordered by road time; the unpriced remainder keeps its straight-line order behind them");
        candidates.Take(2).Should().OnlyContain(c => c.TravelDurationSeconds != null);
        candidates.Skip(2).Should().OnlyContain(c => c.TravelDurationSeconds == null);
    }

    /// <summary>
    /// The radius is a cost cap, not an eligibility filter: the only candidate
    /// a booking has is still the candidate it gets, however far away it is -
    /// the radius only decides that measuring it exactly is not worth paying
    /// for.
    /// </summary>
    [Fact]
    public async Task FindCandidatesAsync_still_returns_a_candidate_beyond_the_route_ranking_radius_without_pricing_it()
    {
        Fixture f;
        Provider distant;
        Guid bookingId;
        using (var context = _db.CreateContext())
        {
            f = Seed(context);
            distant = AddEligibleProvider(context, f, DistantLatitude, DistantLongitude);
            bookingId = AddBooking(context, f);
            context.SaveChanges();
        }

        var routes = new RecordingRouteEstimateProvider(new Dictionary<GeoCoordinate, RoadLeg>());

        using var readContext = _db.CreateContext();
        var candidates = await BuildService(readContext, routes).FindCandidatesAsync(bookingId);

        candidates.Should().ContainSingle(c => c.ProviderId == distant.Id, "distance never removes a candidate from the eligible pool");
        candidates[0].TravelDurationSeconds.Should().BeNull();
        candidates[0].DistanceKm.Should().BeGreaterThan(new AutoAssignmentOptions().RouteRankingRadiusKm);
        routes.Calls.Should().BeEmpty("nothing was inside the radius, so there was nothing worth paying to measure");
    }

    /// <summary>
    /// A sandbox-only response carries no road information - it is the same
    /// Haversine number rescaled - so it must not reorder anything. Ranking by
    /// it would be strictly worse than ranking by distance: whole-second
    /// rounding turns candidates metres apart into ties that then resolve by
    /// Provider.Id.
    /// </summary>
    [Fact]
    public async Task FindCandidatesAsync_keeps_the_straight_line_order_when_every_estimate_degraded_to_the_sandbox()
    {
        Fixture f;
        Provider nearestByAir, fartherByAir;
        Guid bookingId;
        using (var context = _db.CreateContext())
        {
            f = Seed(context);
            nearestByAir = AddEligibleProvider(context, f, BookingLatitude, OneKilometreWest);
            fartherByAir = AddEligibleProvider(context, f, BookingLatitude, FiveKilometresWest);
            bookingId = AddBooking(context, f);
            context.SaveChanges();
        }

        // Durations that would reverse the order if they were believed.
        var routes = new RecordingRouteEstimateProvider(new Dictionary<GeoCoordinate, RoadLeg>
        {
            [At(nearestByAir)] = new(SlowRoadSeconds, RouteEstimateSource.Sandbox),
            [At(fartherByAir)] = new(FastRoadSeconds, RouteEstimateSource.Sandbox)
        });

        using var readContext = _db.CreateContext();
        var candidates = await BuildService(readContext, routes).FindCandidatesAsync(bookingId);

        candidates.Select(c => c.ProviderId).Should().Equal([nearestByAir.Id, fartherByAir.Id]);
        candidates.Should().OnlyContain(c => c.TravelDurationSeconds == null, "an approximation derived from the distance is not a measurement of the road");
    }

    /// <summary>
    /// A partially degraded response is not the same thing: real road data
    /// exists for some candidates, and the sandbox figures beside it are
    /// comparable seconds-of-driving. Throwing all of it away because one
    /// destination was unroutable would be the worse trade.
    /// </summary>
    [Fact]
    public async Task FindCandidatesAsync_ranks_by_travel_time_when_only_some_estimates_degraded_to_the_sandbox()
    {
        Fixture f;
        Provider nearestByAir, fastestByRoad;
        Guid bookingId;
        using (var context = _db.CreateContext())
        {
            f = Seed(context);
            nearestByAir = AddEligibleProvider(context, f, BookingLatitude, OneKilometreWest);
            fastestByRoad = AddEligibleProvider(context, f, BookingLatitude, FiveKilometresWest);
            bookingId = AddBooking(context, f);
            context.SaveChanges();
        }

        var routes = new RecordingRouteEstimateProvider(new Dictionary<GeoCoordinate, RoadLeg>
        {
            [At(nearestByAir)] = new(SlowRoadSeconds, RouteEstimateSource.Sandbox),
            [At(fastestByRoad)] = new(FastRoadSeconds, RouteEstimateSource.GoogleMaps)
        });

        using var readContext = _db.CreateContext();
        var candidates = await BuildService(readContext, routes).FindCandidatesAsync(bookingId);

        candidates.Select(c => c.ProviderId).Should().Equal([fastestByRoad.Id, nearestByAir.Id]);
        candidates.Should().OnlyContain(c => c.TravelDurationSeconds != null);
    }
}

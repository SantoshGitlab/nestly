using FluentAssertions;
using Microsoft.Extensions.Options;
using Nestly.Application;
using Nestly.Application.ProviderManagement;
using Nestly.Application.Routing;
using Nestly.Domain;
using Nestly.Infrastructure.Options;
using Nestly.Infrastructure.Persistence;
using Nestly.Infrastructure.Persistence.Repositories;
using Nestly.Infrastructure.Services;

namespace Nestly.Catalog.Tests;

/// <summary>
/// Task 289: a provider finishing at 11:00 across the city cannot start an
/// 11:00 job. Task 288 stopped slots that literally overlap; the defect these
/// tests pin is the one left behind - two jobs that do not overlap on the
/// clock, but are further apart on the road than the gap between them, which
/// nothing in the eligibility gate looked at.
///
/// Every test drives a stubbed <see cref="IRouteEstimateProvider"/> or the
/// local sandbox estimator - never real HTTP - so the road network can be made
/// to disagree with the map on purpose.
/// </summary>
public sealed class ProviderTravelBufferTests : IClassFixture<TestDatabase>
{
    private readonly TestDatabase _db;

    public ProviderTravelBufferTests(TestDatabase db) => _db = db;

    private static readonly DateOnly SlotDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10));

    private static readonly TimeSpan NineAm = TimeSpan.FromHours(9);
    private static readonly TimeSpan TenAm = TimeSpan.FromHours(10);
    private static readonly TimeSpan ElevenAm = TimeSpan.FromHours(11);
    private static readonly TimeSpan Noon = TimeSpan.FromHours(12);
    private static readonly TimeSpan OnePm = TimeSpan.FromHours(13);
    private static readonly TimeSpan TwoPm = TimeSpan.FromHours(14);

    // Two addresses in Bengaluru, ~1.1 km apart by air, and one in Chennai
    // ~290 km away - far enough that the sandbox estimator alone (Haversine x
    // 1.3 at 25 km/h) puts the drive at many hours, which is what the
    // degradation tests below lean on.
    private static readonly GeoCoordinate Koramangala = new(12.9352m, 77.6245m);
    private static readonly GeoCoordinate Indiranagar = new(12.9352m, 77.6145m);
    private static readonly GeoCoordinate Chennai = new(13.0827m, 80.2707m);

    private const int FortyFiveMinutes = 45 * 60;
    private const int TenMinutes = 10 * 60;

    /// <summary>
    /// A router answering from a table keyed by destination coordinate, which
    /// records every request so the batching and the lookup cap can be asserted
    /// on directly. Destinations it has no entry for get
    /// <paramref name="fallbackDurationSeconds"/>, so a test only has to
    /// configure the leg it cares about.
    /// </summary>
    private sealed class RecordingRouteEstimateProvider(
        IReadOnlyDictionary<GeoCoordinate, int> durationsByDestination,
        int fallbackDurationSeconds = 0,
        RouteEstimateSource source = RouteEstimateSource.GoogleMaps,
        bool returnNothing = false) : IRouteEstimateProvider
    {
        public List<(GeoCoordinate Origin, IReadOnlyList<GeoCoordinate> Destinations)> Calls { get; } = [];

        public int DestinationsRequested => Calls.Sum(call => call.Destinations.Count);

        public Task<IReadOnlyList<RouteEstimate>> EstimateAsync(
            GeoCoordinate origin,
            IReadOnlyList<GeoCoordinate> destinations,
            CancellationToken cancellationToken = default)
        {
            Calls.Add((origin, destinations));

            if (returnNothing)
            {
                // Not a shape either shipped implementation can produce - the
                // point is that the check must not trust that it can't happen.
                return Task.FromResult<IReadOnlyList<RouteEstimate>>([]);
            }

            IReadOnlyList<RouteEstimate> estimates = destinations
                .Select((destination, index) => new RouteEstimate(
                    index,
                    DurationFor(destination) * 8,
                    DurationFor(destination),
                    source))
                .ToList();

            return Task.FromResult(estimates);
        }

        private int DurationFor(GeoCoordinate destination) =>
            durationsByDestination.TryGetValue(destination, out int seconds) ? seconds : fallbackDurationSeconds;
    }

    private sealed record Fixture(Guid CustomerId, Guid CategoryId, Guid ServiceId, Guid CityId, string PincodeCode);

    private static Fixture Seed(NestlyDbContext context)
    {
        string pincodeCode = Guid.NewGuid().ToString("N")[..6];
        var customer = new Customer(Guid.NewGuid(), "9" + Guid.NewGuid().ToString("N")[..9], "Asha Rao", CustomerStatus.Active);
        var state = new State(Guid.NewGuid(), "Karnataka", "KA" + Guid.NewGuid().ToString("N")[..6]);
        var city = new City(Guid.NewGuid(), state.Id, "Bengaluru");
        var pincode = new Pincode(Guid.NewGuid(), city.Id, pincodeCode);
        var category = new Category(Guid.NewGuid(), "Cleaning", "cleaning-" + Guid.NewGuid(), "desc");
        var service = new Service(Guid.NewGuid(), category.Id, "Deep Clean", "deep-clean-" + Guid.NewGuid(), "desc", 500m);

        context.Add(customer);
        context.States.Add(state);
        context.Cities.Add(city);
        context.Pincodes.Add(pincode);
        context.Add(category);
        context.Add(service);
        context.SaveChanges();

        return new Fixture(customer.Id, category.Id, service.Id, city.Id, pincodeCode);
    }

    private static Booking AddBooking(
        NestlyDbContext context, Fixture f, TimeSpan start, TimeSpan end, GeoCoordinate address, DateOnly? date = null)
    {
        var windowId = Guid.NewGuid();
        context.SlotWindows.Add(new SlotWindow(windowId, f.CityId, "Window", start, end));

        var addressSnapshot = new AddressSnapshot(
            "Home", "221B Baker Street", null, null, f.PincodeCode, "Bengaluru", "Karnataka",
            address.Latitude, address.Longitude, "Asha Rao", "9876543210");
        var slot = new SlotSnapshot(windowId, date ?? SlotDate, "Window", start, end);
        var price = new PriceSnapshot(500m, 1, 500m, 0m, 50m, 550m, 18m, 99m, 10m, 659m);

        var booking = new Booking(Guid.NewGuid(), f.CustomerId, new CustomerSnapshot("Asha Rao", "9876543210"), null, addressSnapshot, slot, price);
        booking.AddItem(Guid.NewGuid(), f.ServiceId, "Deep Clean", "deep-clean", 500m, 1);
        booking.TransitionTo(BookingStatus.PaymentPending);
        booking.TransitionTo(BookingStatus.Confirmed);
        booking.TransitionTo(BookingStatus.AwaitingFulfilment);
        context.Add(booking);
        context.SaveChanges();
        return booking;
    }

    private static Provider AddActiveProvider(NestlyDbContext context, Fixture f)
    {
        var provider = new Provider(Guid.NewGuid(), "Ravi Kumar", "Ravi's Repairs", ProviderType.Individual, "+9198" + Guid.NewGuid().ToString("N")[..8]);
        provider.ChangeStatus(ProviderStatus.Active);
        provider.UpdateLocation(Koramangala.Latitude, Koramangala.Longitude);
        context.Add(provider);
        context.Add(new ProviderSkillMapping(Guid.NewGuid(), provider.Id, f.CategoryId));
        context.Add(new ProviderServiceArea(Guid.NewGuid(), provider.Id, f.CityId));
        // Wide enough for every slot these tests use, so availability is never
        // the reason a candidate is refused - this suite is about travel alone.
        foreach (var day in Enum.GetValues<DayOfWeek>())
        {
            context.Add(new ProviderAvailabilityWindow(Guid.NewGuid(), provider.Id, day, TimeSpan.FromHours(6), TimeSpan.FromHours(22)));
        }
        context.SaveChanges();
        return provider;
    }

    private static void AssignLive(NestlyDbContext context, Booking booking, Guid providerId)
    {
        context.Add(new BookingProviderAssignment(Guid.NewGuid(), booking.Id, providerId, BookingAssignedByType.System, null, null));
        booking.AssignProvider(providerId);
        context.SaveChanges();
    }

    private static ProviderAssignmentEligibilityService BuildEligibilityService(
        NestlyDbContext context, IProviderTravelFeasibilityService travelFeasibilityService) => new(
            new BookingRepository(context),
            new ProviderAvailabilityWindowRepository(context),
            new ProviderBlackoutDateRepository(context),
            new ProviderCapacityRepository(context),
            new ProviderScheduleConflictService(context, TestServices.Occupancy()),
            travelFeasibilityService,
            context);

    private sealed record Neighbour(TimeSpan Start, TimeSpan End, GeoCoordinate Address, DateOnly? Date = null);

    /// <summary>
    /// Live-assigns the provider to every <paramref name="neighbours"/> job and
    /// returns a not-yet-assigned candidate booking at
    /// <paramref name="candidateStart"/>-<paramref name="candidateEnd"/> for the
    /// assertion to attempt. No slot ever overlaps another here: task 288's
    /// refusal would mask this one's.
    /// </summary>
    private async Task<(Guid ProviderId, Guid CandidateBookingId)> SeedDayAsync(
        TimeSpan candidateStart, TimeSpan candidateEnd, GeoCoordinate candidateAddress, params Neighbour[] neighbours)
    {
        using var context = _db.CreateContext();
        var f = Seed(context);
        var provider = AddActiveProvider(context, f);

        foreach (var neighbour in neighbours)
        {
            var booking = AddBooking(context, f, neighbour.Start, neighbour.End, neighbour.Address, neighbour.Date);
            AssignLive(context, booking, provider.Id);
        }

        var candidate = AddBooking(context, f, candidateStart, candidateEnd, candidateAddress);
        await context.SaveChangesAsync();

        return (provider.Id, candidate.Id);
    }

    private async Task<bool> IsEligibleAsync(
        (Guid ProviderId, Guid CandidateBookingId) seed,
        IRouteEstimateProvider router,
        AutoAssignmentOptions? options = null)
    {
        using var context = _db.CreateContext();
        var travel = TravelFeasibilityFactory.Build(
            context, router, new SandboxRouteEstimateProvider(Options.Create(new SandboxRouteEstimateOptions())), options);
        return await BuildEligibilityService(context, travel).IsEligibleAsync(seed.ProviderId, seed.CandidateBookingId);
    }

    private static RecordingRouteEstimateProvider RouterTaking(int durationSeconds, params GeoCoordinate[] destinations) =>
        new(destinations.ToDictionary(destination => destination, _ => durationSeconds));

    // ---------------------------------------------------------------------
    // The rule itself.
    // ---------------------------------------------------------------------

    /// <summary>
    /// The case this task exists for: 09:00-11:00 in Indiranagar, then
    /// 11:00-13:00 in Koramangala. Task 288 lets it through - the slots only
    /// touch at an endpoint - but the drive is 45 minutes and the gap is zero.
    /// </summary>
    [Fact]
    public async Task IsEligible_false_when_the_previous_job_is_too_far_away_to_arrive_on_time()
    {
        var seed = await SeedDayAsync(ElevenAm, OnePm, Koramangala, new Neighbour(NineAm, ElevenAm, Indiranagar));

        (await IsEligibleAsync(seed, RouterTaking(FortyFiveMinutes, Indiranagar)))
            .Should().BeFalse("a 45-minute drive does not fit in a zero-minute gap, however legal the slots look on the clock");
    }

    /// <summary>A two-hour gap swallows a 45-minute drive and the 15-minute handover buffer with room to spare.</summary>
    [Fact]
    public async Task IsEligible_true_when_the_gap_comfortably_covers_the_drive_and_the_buffer()
    {
        var seed = await SeedDayAsync(Noon, OnePm, Koramangala, new Neighbour(NineAm, TenAm, Indiranagar));

        (await IsEligibleAsync(seed, RouterTaking(FortyFiveMinutes, Indiranagar))).Should().BeTrue();
    }

    /// <summary>
    /// The boundary the buffer defines: the same 45-minute drive into a
    /// 50-minute gap fails only because parking and handover need the other
    /// fifteen. Without the buffer this case would pass, which is what makes it
    /// a test of the buffer rather than of the drive.
    /// </summary>
    [Fact]
    public async Task IsEligible_false_when_only_the_handover_buffer_pushes_the_leg_past_the_gap()
    {
        var seed = await SeedDayAsync(
            TenAm + TimeSpan.FromMinutes(50), Noon, Koramangala, new Neighbour(NineAm, TenAm, Indiranagar));

        (await IsEligibleAsync(seed, RouterTaking(FortyFiveMinutes, Indiranagar)))
            .Should().BeFalse("45 minutes of driving plus a 15-minute handover does not fit in 50 minutes");

        (await IsEligibleAsync(seed, RouterTaking(FortyFiveMinutes, Indiranagar), new AutoAssignmentOptions { TravelHandoverBufferMinutes = 0 }))
            .Should().BeTrue("with the buffer configured away, 45 minutes of driving fits in 50");
    }

    /// <summary>
    /// Task 288 deliberately keeps back-to-back slots legal, and the buffer
    /// must not quietly take that back: two jobs at one address involve no
    /// drive, so there is nothing to park, nothing to travel and no gap to
    /// require.
    /// </summary>
    [Fact]
    public async Task IsEligible_true_for_back_to_back_jobs_at_the_same_address()
    {
        var seed = await SeedDayAsync(ElevenAm, OnePm, Koramangala, new Neighbour(NineAm, ElevenAm, Koramangala));

        (await IsEligibleAsync(seed, RouterTaking(0, Koramangala)))
            .Should().BeTrue("the next flat in the same building is not a journey - the handover buffer applies to drives, not to doorways");
    }

    /// <summary>The mirror of the first case, and the direction it is easiest to forget: the provider can get here, but then cannot get away.</summary>
    [Fact]
    public async Task IsEligible_false_when_the_following_job_cannot_be_reached_from_this_one()
    {
        var seed = await SeedDayAsync(NineAm, ElevenAm, Koramangala, new Neighbour(ElevenAm, OnePm, Indiranagar));

        (await IsEligibleAsync(seed, RouterTaking(FortyFiveMinutes, Indiranagar)))
            .Should().BeFalse("the job after this one starts the moment it ends, 45 minutes away");
    }

    /// <summary>Only the immediately adjacent jobs matter - a job three hours later is separated by its own gap, not this one's.</summary>
    [Fact]
    public async Task IsEligible_true_when_the_far_away_job_is_not_the_adjacent_one()
    {
        var seed = await SeedDayAsync(
            ElevenAm, Noon, Koramangala,
            new Neighbour(NineAm, TenAm, Chennai),
            new Neighbour(TenAm + TimeSpan.FromMinutes(30), ElevenAm, Koramangala));

        (await IsEligibleAsync(seed, RouterTaking(0, Koramangala)))
            .Should().BeTrue("the Chennai job is not adjacent to this booking - the 10:30 job at the same address is");
    }

    /// <summary>Yesterday's job across the country is nobody's drive: adjacency is within one <c>SlotDate</c>.</summary>
    [Fact]
    public async Task IsEligible_true_when_the_only_distant_job_is_on_another_day()
    {
        var router = RouterTaking(FortyFiveMinutes, Chennai);
        var seed = await SeedDayAsync(
            NineAm, ElevenAm, Koramangala,
            new Neighbour(TwoPm, TwoPm + TimeSpan.FromHours(1), Chennai, SlotDate.AddDays(-1)));

        (await IsEligibleAsync(seed, router)).Should().BeTrue();
        router.Calls.Should().BeEmpty("a candidate with no adjacent job that day has no leg to price");
    }

    // ---------------------------------------------------------------------
    // The kill switch.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task IsEligible_true_when_the_kill_switch_is_off_however_far_the_previous_job_is()
    {
        var router = RouterTaking(FortyFiveMinutes, Indiranagar);
        var seed = await SeedDayAsync(ElevenAm, OnePm, Koramangala, new Neighbour(NineAm, ElevenAm, Indiranagar));

        (await IsEligibleAsync(seed, router, new AutoAssignmentOptions { TravelBufferEnabled = false }))
            .Should().BeTrue("with the switch off the gate is exactly what it was before task 289");
        router.Calls.Should().BeEmpty("a disabled check must not still be paying for route lookups");
    }

    // ---------------------------------------------------------------------
    // Degradation. Getting this backwards is the expensive mistake.
    // ---------------------------------------------------------------------

    /// <summary>
    /// The maps provider answers with nothing usable. The check must fall back
    /// to the sandbox estimate - Bengaluru to Chennai is hours of driving by
    /// any measure - and refuse, not shrug and allow.
    /// </summary>
    [Fact]
    public async Task IsEligible_false_when_the_router_returns_nothing_and_the_sandbox_says_the_drive_is_impossible()
    {
        var router = new RecordingRouteEstimateProvider(new Dictionary<GeoCoordinate, int>(), returnNothing: true);
        var seed = await SeedDayAsync(ElevenAm, OnePm, Koramangala, new Neighbour(NineAm, ElevenAm, Chennai));

        (await IsEligibleAsync(seed, router))
            .Should().BeFalse("an unusable routing response is not permission to double-book the city");
        router.Calls.Should().HaveCount(1, "the provider was asked - it simply could not answer");
    }

    /// <summary>
    /// The sandbox is not "no information" here, unlike task 267's ranking: an
    /// all-sandbox response is the only travel estimate there is, and a
    /// four-hour approximate drive still does not fit in a zero-minute gap.
    /// </summary>
    [Fact]
    public async Task IsEligible_false_when_every_estimate_degraded_to_the_sandbox()
    {
        var router = new RecordingRouteEstimateProvider(
            new Dictionary<GeoCoordinate, int> { [Indiranagar] = FortyFiveMinutes }, source: RouteEstimateSource.Sandbox);
        var seed = await SeedDayAsync(ElevenAm, OnePm, Koramangala, new Neighbour(NineAm, ElevenAm, Indiranagar));

        (await IsEligibleAsync(seed, router)).Should().BeFalse();
    }

    /// <summary>A degraded estimate is still an estimate: a sandbox-sourced leg that does fit is not a refusal either.</summary>
    [Fact]
    public async Task IsEligible_true_when_a_sandbox_sourced_leg_fits_the_gap()
    {
        var router = new RecordingRouteEstimateProvider(
            new Dictionary<GeoCoordinate, int> { [Indiranagar] = TenMinutes }, source: RouteEstimateSource.Sandbox);
        var seed = await SeedDayAsync(Noon, OnePm, Koramangala, new Neighbour(NineAm, TenAm, Indiranagar));

        (await IsEligibleAsync(seed, router)).Should().BeTrue();
    }

    // ---------------------------------------------------------------------
    // Cost.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Both_neighbouring_legs_are_priced_in_one_batched_request()
    {
        var router = RouterTaking(TenMinutes, Indiranagar);
        var seed = await SeedDayAsync(
            ElevenAm, Noon, Koramangala,
            new Neighbour(NineAm, TenAm, Indiranagar),
            new Neighbour(OnePm, TwoPm, Indiranagar));

        (await IsEligibleAsync(seed, router)).Should().BeTrue();
        router.Calls.Should().HaveCount(1, "one origin, both neighbours - the routing seam batches exactly this");
        router.Calls[0].Destinations.Should().HaveCount(2);
        router.Calls[0].Origin.Should().Be(Koramangala, "the booking's own address is the single origin both legs are measured from");
    }

    /// <summary>
    /// An eligibility pass over many candidates must not fan out into unbounded
    /// billed calls - and the candidates past the cap must not sail through
    /// unchecked either. With a one-lookup budget the second and third
    /// candidates are measured by the local sandbox estimator instead, which
    /// still puts a Chennai-to-Koramangala drive far outside a zero-minute gap.
    /// </summary>
    [Fact]
    public async Task The_lookup_cap_bounds_one_pass_without_letting_later_candidates_through()
    {
        var router = RouterTaking(FortyFiveMinutes, Chennai);

        var seeds = new List<(Guid ProviderId, Guid CandidateBookingId)>();
        for (int candidate = 0; candidate < 3; candidate++)
        {
            seeds.Add(await SeedDayAsync(ElevenAm, OnePm, Koramangala, new Neighbour(NineAm, ElevenAm, Chennai)));
        }

        using var context = _db.CreateContext();

        // One travel-feasibility instance across all three candidates - which
        // is what a real pass gets, since ProviderAssignmentEligibilityService
        // and this service share one DI scope for the whole pass.
        var travel = TravelFeasibilityFactory.Build(
            context,
            router,
            new SandboxRouteEstimateProvider(Options.Create(new SandboxRouteEstimateOptions())),
            new AutoAssignmentOptions { MaxTravelRouteLookups = 1 });
        var eligibility = BuildEligibilityService(context, travel);

        foreach (var seed in seeds)
        {
            (await eligibility.IsEligibleAsync(seed.ProviderId, seed.CandidateBookingId))
                .Should().BeFalse("running out of budget is a reason to stop spending, never a reason to allow an impossible assignment");
        }

        router.DestinationsRequested.Should().Be(1, "the pass may bill for exactly its configured budget and no more");
    }

    /// <summary>A zero budget is a legitimate posture: enforce the rule on the free approximation and never call the maps provider at all.</summary>
    [Fact]
    public async Task A_zero_lookup_budget_still_enforces_the_rule_on_the_sandbox_estimate()
    {
        var router = RouterTaking(0, Chennai);
        var seed = await SeedDayAsync(ElevenAm, OnePm, Koramangala, new Neighbour(NineAm, ElevenAm, Chennai));

        (await IsEligibleAsync(seed, router, new AutoAssignmentOptions { MaxTravelRouteLookups = 0 })).Should().BeFalse();
        router.Calls.Should().BeEmpty();
    }
}

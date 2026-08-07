using FluentAssertions;
using Microsoft.Extensions.Options;
using Nestly.Application;
using Nestly.Application.Bookings;
using Nestly.Domain;
using Nestly.Infrastructure.Options;
using Nestly.Infrastructure.Persistence.Repositories;
using Nestly.Infrastructure.Services;

namespace Nestly.Catalog.Tests;

/// <summary>
/// Covers task 244: candidate discovery (skill + service-area match) and
/// distance ranking for the automatic-assignment engine (PROVIDER.md OPEN
/// DECISIONS - AUTOMATIC ASSIGNMENT).
///
/// Task 267's road-travel-time ranking is covered separately in
/// <see cref="ProviderMatchingServiceRouteRankingTests"/>; these tests keep
/// asserting the straight-line behaviour, which is still what the service
/// does whenever no real road data is available.
/// </summary>
public sealed class ProviderMatchingServiceTests : IClassFixture<TestDatabase>
{
    private readonly TestDatabase _db;

    public ProviderMatchingServiceTests(TestDatabase db) => _db = db;

    /// <summary>
    /// Task 267 gave the service a routing dependency. These tests use the
    /// real <see cref="SandboxRouteEstimateProvider"/> - no HTTP, no key, no
    /// stub - precisely because a sandbox-only response is defined to carry no
    /// road information: the service ignores it and every assertion below
    /// stays an assertion about straight-line ranking, unchanged by task 267.
    /// </summary>
    private static ProviderMatchingService BuildService(Nestly.Infrastructure.Persistence.NestlyDbContext context) => new(
        new BookingRepository(context),
        context,
        new SandboxRouteEstimateProvider(Options.Create(new SandboxRouteEstimateOptions())),
        Options.Create(new AutoAssignmentOptions()));

    private sealed record Fixture(
        Customer Customer, City City, Pincode Pincode, Category Category, Service Service, SlotWindow Window);

    private static Fixture Seed(Nestly.Infrastructure.Persistence.NestlyDbContext context)
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

    private const decimal DefaultLatitude = 12.9352m;
    private const decimal DefaultLongitude = 77.6245m;

    private static Booking NewBooking(Fixture f, decimal lat = DefaultLatitude, decimal lng = DefaultLongitude)
    {
        var address = new AddressSnapshot(
            "Home", "221B Baker Street", null, null, f.Pincode.Code, f.City.Name, "Karnataka",
            lat, lng, "Asha Rao", "9876543210");
        var slot = new SlotSnapshot(f.Window.Id, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)), "Morning", TimeSpan.FromHours(9), TimeSpan.FromHours(13));
        var price = new PriceSnapshot(500m, 1, 500m, 0m, 50m, 550m, 18m, 99m, 10m, 659m);
        var booking = new Booking(Guid.NewGuid(), f.Customer.Id, new CustomerSnapshot(f.Customer.Name, f.Customer.Mobile), null, address, slot, price);
        booking.AddItem(Guid.NewGuid(), f.Service.Id, f.Service.Name, f.Service.Slug, 500m, 1);
        return booking;
    }

    private static Provider NewActiveProvider(decimal? lat = null, decimal? lng = null)
    {
        var provider = new Provider(Guid.NewGuid(), "Ravi Kumar", "Ravi's Repairs", ProviderType.Individual, "+9198" + Guid.NewGuid().ToString("N")[..8]);
        provider.ChangeStatus(ProviderStatus.Active);
        provider.UpdateLocation(lat, lng);
        return provider;
    }

    [Fact]
    public async Task FindCandidatesAsync_returns_a_provider_matching_skill_and_city()
    {
        Fixture f;
        Provider provider;
        Guid bookingId;
        using (var context = _db.CreateContext())
        {
            f = Seed(context);
            provider = NewActiveProvider();
            context.Add(provider);
            context.Add(new ProviderSkillMapping(Guid.NewGuid(), provider.Id, f.Category.Id));
            context.Add(new ProviderServiceArea(Guid.NewGuid(), provider.Id, f.City.Id));

            var booking = NewBooking(f);
            await new BookingRepository(context).AddAsync(booking);
            bookingId = booking.Id;
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        var service = BuildService(readContext);
        var candidates = await service.FindCandidatesAsync(bookingId);

        candidates.Should().ContainSingle(c => c.ProviderId == provider.Id);
    }

    [Fact]
    public async Task FindCandidatesAsync_excludes_a_provider_with_no_matching_skill()
    {
        Fixture f;
        Guid bookingId;
        using (var context = _db.CreateContext())
        {
            f = Seed(context);
            var provider = NewActiveProvider();
            var unrelatedCategory = new Category(Guid.NewGuid(), "Plumbing", "plumbing-" + Guid.NewGuid(), "desc");
            context.Add(unrelatedCategory);
            context.Add(provider);
            context.Add(new ProviderSkillMapping(Guid.NewGuid(), provider.Id, unrelatedCategory.Id));
            context.Add(new ProviderServiceArea(Guid.NewGuid(), provider.Id, f.City.Id));

            var booking = NewBooking(f);
            await new BookingRepository(context).AddAsync(booking);
            bookingId = booking.Id;
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        var service = BuildService(readContext);
        var candidates = await service.FindCandidatesAsync(bookingId);

        candidates.Should().BeEmpty();
    }

    [Fact]
    public async Task FindCandidatesAsync_excludes_a_provider_whose_service_area_is_a_different_city()
    {
        Fixture f;
        Guid bookingId;
        using (var context = _db.CreateContext())
        {
            f = Seed(context);
            var otherState = new State(Guid.NewGuid(), "Maharashtra", "MH" + Guid.NewGuid().ToString("N")[..6]);
            var otherCity = new City(Guid.NewGuid(), otherState.Id, "Mumbai");
            context.States.Add(otherState);
            context.Cities.Add(otherCity);

            var provider = NewActiveProvider();
            context.Add(provider);
            context.Add(new ProviderSkillMapping(Guid.NewGuid(), provider.Id, f.Category.Id));
            context.Add(new ProviderServiceArea(Guid.NewGuid(), provider.Id, otherCity.Id));

            var booking = NewBooking(f);
            await new BookingRepository(context).AddAsync(booking);
            bookingId = booking.Id;
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        var service = BuildService(readContext);
        var candidates = await service.FindCandidatesAsync(bookingId);

        candidates.Should().BeEmpty();
    }

    [Fact]
    public async Task FindCandidatesAsync_respects_pincode_scoped_service_areas()
    {
        Fixture f;
        Provider matchingProvider;
        Provider wrongPincodeProvider;
        Guid bookingId;
        using (var context = _db.CreateContext())
        {
            f = Seed(context);
            var otherPincode = new Pincode(Guid.NewGuid(), f.City.Id, Guid.NewGuid().ToString("N")[..6]);
            context.Pincodes.Add(otherPincode);

            matchingProvider = NewActiveProvider();
            wrongPincodeProvider = NewActiveProvider();
            context.Add(matchingProvider);
            context.Add(wrongPincodeProvider);
            context.Add(new ProviderSkillMapping(Guid.NewGuid(), matchingProvider.Id, f.Category.Id));
            context.Add(new ProviderSkillMapping(Guid.NewGuid(), wrongPincodeProvider.Id, f.Category.Id));
            context.Add(new ProviderServiceArea(Guid.NewGuid(), matchingProvider.Id, f.City.Id, pincodeId: f.Pincode.Id));
            context.Add(new ProviderServiceArea(Guid.NewGuid(), wrongPincodeProvider.Id, f.City.Id, pincodeId: otherPincode.Id));

            var booking = NewBooking(f);
            await new BookingRepository(context).AddAsync(booking);
            bookingId = booking.Id;
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        var service = BuildService(readContext);
        var candidates = await service.FindCandidatesAsync(bookingId);

        candidates.Should().ContainSingle(c => c.ProviderId == matchingProvider.Id);
    }

    [Fact]
    public async Task FindCandidatesAsync_ranks_the_nearer_provider_first_and_puts_unlocated_providers_last()
    {
        Fixture f;
        Provider near, far, unlocated;
        Guid bookingId;
        using (var context = _db.CreateContext())
        {
            f = Seed(context);

            // Booking address: Koramangala, Bengaluru.
            near = NewActiveProvider(12.9352m, 77.6146m);   // ~2km away
            far = NewActiveProvider(13.0827m, 80.2707m);    // Chennai - ~290km away
            unlocated = NewActiveProvider(null, null);

            foreach (var provider in new[] { near, far, unlocated })
            {
                context.Add(provider);
                context.Add(new ProviderSkillMapping(Guid.NewGuid(), provider.Id, f.Category.Id));
                context.Add(new ProviderServiceArea(Guid.NewGuid(), provider.Id, f.City.Id));
            }

            var booking = NewBooking(f);
            await new BookingRepository(context).AddAsync(booking);
            bookingId = booking.Id;
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        var service = BuildService(readContext);
        var candidates = await service.FindCandidatesAsync(bookingId);

        candidates.Should().HaveCount(3);
        candidates[0].ProviderId.Should().Be(near.Id);
        candidates[0].DistanceKm.Should().BeLessThan(candidates[1].DistanceKm!.Value);
        candidates[1].ProviderId.Should().Be(far.Id);
        candidates[2].ProviderId.Should().Be(unlocated.Id, "a provider with no coordinates has no distance to rank by, so sorts last rather than being excluded");
        candidates[2].DistanceKm.Should().BeNull();
    }

    /// <summary>Task 250: an exact-distance tie must resolve the same way every run - no rating input exists to break it with (PROVIDER.md OPEN DECISIONS - AUTOMATIC ASSIGNMENT #3, corrected while implementing task 244), so it falls back to Provider.Id, which is at least deterministic even though it carries no ranking meaning.</summary>
    [Fact]
    public async Task FindCandidatesAsync_breaks_an_exact_distance_tie_deterministically_by_provider_id()
    {
        Fixture f;
        Provider first, second;
        Guid bookingId;
        using (var context = _db.CreateContext())
        {
            f = Seed(context);

            // Identical coordinates -> identical (zero) distance from the booking's own address.
            first = NewActiveProvider(12.9352m, 77.6245m);
            second = NewActiveProvider(12.9352m, 77.6245m);

            foreach (var provider in new[] { first, second })
            {
                context.Add(provider);
                context.Add(new ProviderSkillMapping(Guid.NewGuid(), provider.Id, f.Category.Id));
                context.Add(new ProviderServiceArea(Guid.NewGuid(), provider.Id, f.City.Id));
            }

            var booking = NewBooking(f);
            await new BookingRepository(context).AddAsync(booking);
            bookingId = booking.Id;
            context.SaveChanges();
        }

        var expectedOrder = new[] { first.Id, second.Id }.OrderBy(id => id).ToList();

        using var readContext = _db.CreateContext();
        var service = BuildService(readContext);
        var candidates = await service.FindCandidatesAsync(bookingId);

        candidates.Should().HaveCount(2);
        candidates[0].DistanceKm.Should().Be(candidates[1].DistanceKm, "both providers sit at the exact same coordinates");
        candidates.Select(c => c.ProviderId).Should().Equal(expectedOrder);
    }

    [Fact]
    public async Task FindCandidatesAsync_honours_the_exclusion_list()
    {
        Fixture f;
        Provider provider;
        Guid bookingId;
        using (var context = _db.CreateContext())
        {
            f = Seed(context);
            provider = NewActiveProvider();
            context.Add(provider);
            context.Add(new ProviderSkillMapping(Guid.NewGuid(), provider.Id, f.Category.Id));
            context.Add(new ProviderServiceArea(Guid.NewGuid(), provider.Id, f.City.Id));

            var booking = NewBooking(f);
            await new BookingRepository(context).AddAsync(booking);
            bookingId = booking.Id;
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        var service = BuildService(readContext);
        var candidates = await service.FindCandidatesAsync(bookingId, excludeProviderIds: [provider.Id]);

        candidates.Should().BeEmpty();
    }

    [Fact]
    public async Task FindCandidatesAsync_excludes_a_suspended_provider()
    {
        Fixture f;
        Guid bookingId;
        using (var context = _db.CreateContext())
        {
            f = Seed(context);
            var provider = NewActiveProvider();
            provider.ChangeStatus(ProviderStatus.Suspended);
            context.Add(provider);
            context.Add(new ProviderSkillMapping(Guid.NewGuid(), provider.Id, f.Category.Id));
            context.Add(new ProviderServiceArea(Guid.NewGuid(), provider.Id, f.City.Id));

            var booking = NewBooking(f);
            await new BookingRepository(context).AddAsync(booking);
            bookingId = booking.Id;
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        var service = BuildService(readContext);
        var candidates = await service.FindCandidatesAsync(bookingId);

        candidates.Should().BeEmpty();
    }
}

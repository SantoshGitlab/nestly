using FluentAssertions;
using Nestly.Application.Serviceability;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence.Repositories;
using Nestly.Infrastructure.Services;

namespace Nestly.Catalog.Tests;

/// <summary>Covers tasks 45a-d and 46's underlying logic: slot availability calculation.</summary>
public sealed class SlotAvailabilityServiceTests : IClassFixture<TestDatabase>
{
    private readonly TestDatabase _db;

    private SlotAvailabilityService BuildService(Nestly.Infrastructure.Persistence.NestlyDbContext context, TimeProvider? timeProvider = null) => new(
        new ServiceabilityRepository(context),
        new ServiceabilityValidationService(new ServiceabilityRepository(context)),
        new SlotWindowRepository(context),
        new SlotBlackoutRepository(context),
        new SlotBookingPolicyRepository(context),
        timeProvider ?? TimeProvider.System);

    public SlotAvailabilityServiceTests(TestDatabase db) => _db = db;

    private sealed record Fixture(State State, City City, Pincode Pincode, Locality Locality, Category Category, Service Service);

    private Fixture SeedGeographyAndService(Nestly.Infrastructure.Persistence.NestlyDbContext context, DayOfWeek windowDay, TimeSpan start, TimeSpan end, int? capacity = null)
    {
        var state = new State(Guid.NewGuid(), "Karnataka", "KA" + Guid.NewGuid().ToString("N")[..6]);
        var city = new City(Guid.NewGuid(), state.Id, "Bengaluru");
        var zone = new Zone(Guid.NewGuid(), city.Id, "Central");
        var pincode = new Pincode(Guid.NewGuid(), city.Id, Guid.NewGuid().ToString("N")[..6]);
        var locality = new Locality(Guid.NewGuid(), zone.Id, pincode.Id, "Test Locality");
        var category = new Category(Guid.NewGuid(), "Cleaning", "cleaning-" + Guid.NewGuid(), "desc");
        var service = new Service(Guid.NewGuid(), category.Id, "Deep Clean", "deep-clean-" + Guid.NewGuid(), "desc", 499m);
        var window = new SlotWindow(Guid.NewGuid(), city.Id, "Window", start, end);
        if (capacity is not null) window.SetCapacity(capacity);
        var rule = new SlotWindowRule(Guid.NewGuid(), window.Id, windowDay);

        context.States.Add(state);
        context.Cities.Add(city);
        context.Zones.Add(zone);
        context.Pincodes.Add(pincode);
        context.Localities.Add(locality);
        context.Add(category);
        context.Add(service);
        context.ServicePincodeMappings.Add(new ServicePincodeMapping(Guid.NewGuid(), service.Id, pincode.Id));
        context.SlotWindows.Add(window);
        context.SlotWindowRules.Add(rule);
        context.SaveChanges();

        return new Fixture(state, city, pincode, locality, category, service);
    }

    [Fact]
    public async Task Returns_the_window_on_a_matching_day_far_enough_in_the_future()
    {
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10));
        Fixture fixture;
        using (var context = _db.CreateContext())
        {
            fixture = SeedGeographyAndService(context, futureDate.DayOfWeek, TimeSpan.FromHours(9), TimeSpan.FromHours(13));
        }

        using var readContext = _db.CreateContext();
        var result = await BuildService(readContext).GetAvailableSlotsAsync(fixture.Service.Id, fixture.Locality.Id, futureDate);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsServiceable.Should().BeTrue();
        result.Value.Slots.Should().ContainSingle(s => s.SlotWindowId != Guid.Empty);
    }

    [Fact]
    public async Task Not_serviceable_when_service_has_no_mapping_to_the_pincode()
    {
        var state = new State(Guid.NewGuid(), "Maharashtra", "MH" + Guid.NewGuid().ToString("N")[..6]);
        var city = new City(Guid.NewGuid(), state.Id, "Pune");
        var zone = new Zone(Guid.NewGuid(), city.Id, "Central");
        var pincode = new Pincode(Guid.NewGuid(), city.Id, Guid.NewGuid().ToString("N")[..6]);
        var locality = new Locality(Guid.NewGuid(), zone.Id, pincode.Id, "Unserved");
        var category = new Category(Guid.NewGuid(), "Painting", "painting-" + Guid.NewGuid(), "desc");
        var service = new Service(Guid.NewGuid(), category.Id, "Wall Paint", "wall-paint-" + Guid.NewGuid(), "desc", 999m);

        using (var context = _db.CreateContext())
        {
            context.States.Add(state);
            context.Cities.Add(city);
            context.Zones.Add(zone);
            context.Pincodes.Add(pincode);
            context.Localities.Add(locality);
            context.Add(category);
            context.Add(service);
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        var result = await BuildService(readContext).GetAvailableSlotsAsync(service.Id, locality.Id, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)));

        result.IsSuccess.Should().BeTrue();
        result.Value.IsServiceable.Should().BeFalse();
        result.Value.Slots.Should().BeEmpty();
    }

    [Fact]
    public async Task Window_starting_before_the_cutoff_threshold_today_is_excluded()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        Fixture fixture;
        using (var context = _db.CreateContext())
        {
            // A window starting at midnight will always be in the past relative to "now" on the same day.
            fixture = SeedGeographyAndService(context, today.DayOfWeek, TimeSpan.Zero, TimeSpan.FromHours(1));
            context.SlotBookingPolicies.Add(new SlotBookingPolicy(Guid.NewGuid(), fixture.City.Id, 60, 30));
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        var result = await BuildService(readContext).GetAvailableSlotsAsync(fixture.Service.Id, fixture.Locality.Id, today);

        result.IsSuccess.Should().BeTrue();
        result.Value.Slots.Should().BeEmpty();
    }

    [Fact]
    public async Task Date_beyond_max_advance_days_returns_no_slots_but_still_serviceable()
    {
        var farFuture = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(60));
        Fixture fixture;
        using (var context = _db.CreateContext())
        {
            fixture = SeedGeographyAndService(context, farFuture.DayOfWeek, TimeSpan.FromHours(9), TimeSpan.FromHours(13));
            context.SlotBookingPolicies.Add(new SlotBookingPolicy(Guid.NewGuid(), fixture.City.Id, 60, 7));
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        var result = await BuildService(readContext).GetAvailableSlotsAsync(fixture.Service.Id, fixture.Locality.Id, farFuture);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsServiceable.Should().BeTrue();
        result.Value.Slots.Should().BeEmpty();
    }

    [Fact]
    public async Task Blacked_out_date_returns_no_slots()
    {
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));
        Fixture fixture;
        using (var context = _db.CreateContext())
        {
            fixture = SeedGeographyAndService(context, futureDate.DayOfWeek, TimeSpan.FromHours(9), TimeSpan.FromHours(13));
            context.SlotBlackouts.Add(new SlotBlackout(Guid.NewGuid(), fixture.City.Id, futureDate, futureDate, SlotBlackoutType.Holiday, "Test Holiday"));
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        var result = await BuildService(readContext).GetAvailableSlotsAsync(fixture.Service.Id, fixture.Locality.Id, futureDate);

        result.IsSuccess.Should().BeTrue();
        result.Value.Slots.Should().BeEmpty();
    }

    [Fact]
    public async Task Slot_option_reports_the_windows_configured_capacity()
    {
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3));
        Fixture fixture;
        using (var context = _db.CreateContext())
        {
            fixture = SeedGeographyAndService(context, futureDate.DayOfWeek, TimeSpan.FromHours(9), TimeSpan.FromHours(13), capacity: 5);
        }

        using var readContext = _db.CreateContext();
        var result = await BuildService(readContext).GetAvailableSlotsAsync(fixture.Service.Id, fixture.Locality.Id, futureDate);

        result.Value.Slots.Single().MaxBookingsPerSlot.Should().Be(5);
    }

    [Fact]
    public async Task Revalidate_succeeds_for_a_slot_that_is_still_available()
    {
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(4));
        Fixture fixture;
        Guid windowId;
        using (var context = _db.CreateContext())
        {
            fixture = SeedGeographyAndService(context, futureDate.DayOfWeek, TimeSpan.FromHours(9), TimeSpan.FromHours(13));
            windowId = context.SlotWindows.Single(w => w.CityId == fixture.City.Id).Id;
        }

        using var readContext = _db.CreateContext();
        var result = await BuildService(readContext).RevalidateSlotAsync(fixture.Service.Id, fixture.Locality.Id, windowId, futureDate);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsValid.Should().BeTrue();
        result.Value.Reason.Should().BeNull();
    }

    [Fact]
    public async Task Revalidate_fails_once_the_date_becomes_blacked_out()
    {
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(6));
        Fixture fixture;
        Guid windowId;
        using (var context = _db.CreateContext())
        {
            fixture = SeedGeographyAndService(context, futureDate.DayOfWeek, TimeSpan.FromHours(9), TimeSpan.FromHours(13));
            windowId = context.SlotWindows.Single(w => w.CityId == fixture.City.Id).Id;
        }

        using (var context = _db.CreateContext())
        {
            context.SlotBlackouts.Add(new SlotBlackout(Guid.NewGuid(), fixture.City.Id, futureDate, futureDate, SlotBlackoutType.Blackout, "Late admin block"));
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        var result = await BuildService(readContext).RevalidateSlotAsync(fixture.Service.Id, fixture.Locality.Id, windowId, futureDate);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsValid.Should().BeFalse();
        result.Value.Reason.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Unknown_locality_returns_not_found()
    {
        using var context = _db.CreateContext();
        var result = await BuildService(context).GetAvailableSlotsAsync(Guid.NewGuid(), Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Slots.LocalityNotFound");
    }
}

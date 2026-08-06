using FluentAssertions;
using Nestly.Application.Slots;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence.Repositories;
using Nestly.Infrastructure.Services;

namespace Nestly.Catalog.Tests;

/// <summary>Covers tasks 113a-e: admin slot configuration (SRS 12.10) - windows, blackouts, cutoffs, capacity, availability overrides.</summary>
public sealed class SlotManagementServiceTests : IClassFixture<TestDatabase>
{
    private readonly TestDatabase _db;

    public SlotManagementServiceTests(TestDatabase db) => _db = db;

    private SlotManagementService CreateService()
    {
        var context = _db.CreateContext();
        return new SlotManagementService(
            new SlotWindowRepository(context),
            new SlotBlackoutRepository(context),
            new SlotBookingPolicyRepository(context),
            new SlotAvailabilityOverrideRepository(context),
            new CityRepository(context),
            new CategoryRepository(context),
            new ServiceRepository(context));
    }

    private (State state, City city) SeedCity(string cityName)
    {
        var state = new State(Guid.NewGuid(), "Karnataka", "KA" + Guid.NewGuid().ToString("N")[..6]);
        var city = new City(Guid.NewGuid(), state.Id, cityName);
        using var context = _db.CreateContext();
        context.States.Add(state);
        context.Cities.Add(city);
        context.SaveChanges();
        return (state, city);
    }

    // ---- Windows (task 113a) ----

    [Fact]
    public async Task Creating_a_window_persists_its_day_of_week_rules_and_lists_it()
    {
        var (_, city) = SeedCity("Bengaluru");
        var service = CreateService();

        var created = await service.CreateWindowAsync(new SlotWindowCreateRequest(
            city.Id, "Morning", TimeSpan.FromHours(9), TimeSpan.FromHours(13), 10,
            [DayOfWeek.Monday, DayOfWeek.Wednesday]));

        created.IsSuccess.Should().BeTrue();
        created.Value.DaysOfWeek.Should().BeEquivalentTo([DayOfWeek.Monday, DayOfWeek.Wednesday]);
        created.Value.MaxBookingsPerSlot.Should().Be(10);
        created.Value.CityName.Should().Be("Bengaluru");

        var listed = await service.ListWindowsAsync(city.Id);
        listed.Should().ContainSingle(w => w.Id == created.Value.Id);
    }

    [Fact]
    public async Task Creating_a_window_under_a_nonexistent_city_returns_not_found()
    {
        var service = CreateService();

        var result = await service.CreateWindowAsync(new SlotWindowCreateRequest(
            Guid.NewGuid(), "Ghost", TimeSpan.FromHours(9), TimeSpan.FromHours(13), null, []));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Slots.CityNotFound");
    }

    [Fact]
    public async Task Updating_a_window_replaces_its_day_of_week_rules()
    {
        var (_, city) = SeedCity("Chennai");
        var service = CreateService();
        var window = (await service.CreateWindowAsync(new SlotWindowCreateRequest(
            city.Id, "Evening", TimeSpan.FromHours(17), TimeSpan.FromHours(21), null, [DayOfWeek.Monday]))).Value;

        var updated = await service.UpdateWindowAsync(window.Id, new SlotWindowUpdateRequest(
            "Evening Extended", TimeSpan.FromHours(17), TimeSpan.FromHours(22), [DayOfWeek.Friday, DayOfWeek.Saturday]));

        updated.IsSuccess.Should().BeTrue();
        updated.Value.Name.Should().Be("Evening Extended");
        updated.Value.DaysOfWeek.Should().BeEquivalentTo([DayOfWeek.Friday, DayOfWeek.Saturday]);
    }

    [Fact]
    public async Task Setting_capacity_updates_max_bookings_per_slot()
    {
        var (_, city) = SeedCity("Pune");
        var service = CreateService();
        var window = (await service.CreateWindowAsync(new SlotWindowCreateRequest(
            city.Id, "Afternoon", TimeSpan.FromHours(13), TimeSpan.FromHours(17), null, []))).Value;

        var updated = await service.SetWindowCapacityAsync(window.Id, new SlotWindowCapacityUpdateRequest(25));

        updated.IsSuccess.Should().BeTrue();
        updated.Value.MaxBookingsPerSlot.Should().Be(25);
    }

    [Fact]
    public async Task Deactivating_a_window_persists_and_is_reflected_in_the_listing()
    {
        var (_, city) = SeedCity("Hyderabad");
        var service = CreateService();
        var window = (await service.CreateWindowAsync(new SlotWindowCreateRequest(
            city.Id, "Morning", TimeSpan.FromHours(9), TimeSpan.FromHours(13), null, []))).Value;

        (await service.SetWindowActiveAsync(window.Id, false)).IsSuccess.Should().BeTrue();

        var listed = await service.ListWindowsAsync(city.Id);
        listed.Single(w => w.Id == window.Id).IsActive.Should().BeFalse();
    }

    // ---- Blackouts (task 113b) ----

    [Fact]
    public async Task Creating_and_deleting_a_blackout_round_trips()
    {
        var (_, city) = SeedCity("Kolkata");
        var service = CreateService();

        var created = await service.CreateBlackoutAsync(new SlotBlackoutCreateRequest(
            city.Id, new DateOnly(2026, 11, 8), new DateOnly(2026, 11, 9), SlotBlackoutType.Holiday, "Diwali"));

        created.IsSuccess.Should().BeTrue();
        (await service.ListBlackoutsAsync(city.Id)).Should().ContainSingle(b => b.Id == created.Value.Id);

        var deleted = await service.DeleteBlackoutAsync(created.Value.Id);
        deleted.IsSuccess.Should().BeTrue();
        (await service.ListBlackoutsAsync(city.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task Deleting_an_unknown_blackout_returns_not_found()
    {
        var service = CreateService();

        var result = await service.DeleteBlackoutAsync(Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Slots.BlackoutNotFound");
    }

    // ---- Cutoffs / advance-booking policy (task 113c) ----

    [Fact]
    public async Task Upserting_a_booking_policy_creates_then_updates_the_same_city_row()
    {
        var (_, city) = SeedCity("Ahmedabad");
        var service = CreateService();

        var created = await service.UpsertBookingPolicyAsync(new SlotBookingPolicyUpsertRequest(city.Id, 60, 7));
        created.IsSuccess.Should().BeTrue();

        var updated = await service.UpsertBookingPolicyAsync(new SlotBookingPolicyUpsertRequest(city.Id, 90, 14));
        updated.IsSuccess.Should().BeTrue();
        updated.Value.Id.Should().Be(created.Value.Id);
        updated.Value.CutoffMinutes.Should().Be(90);
        updated.Value.MaxAdvanceDays.Should().Be(14);

        (await service.ListBookingPoliciesAsync()).Should().ContainSingle(p => p.CityId == city.Id);
    }

    // ---- Availability overrides (task 113e) ----

    [Fact]
    public async Task Creating_and_deleting_an_availability_override_round_trips()
    {
        var (_, city) = SeedCity("Mumbai");
        var service = CreateService();
        var window = (await service.CreateWindowAsync(new SlotWindowCreateRequest(
            city.Id, "Morning", TimeSpan.FromHours(9), TimeSpan.FromHours(13), null, []))).Value;

        var created = await service.CreateOverrideAsync(new SlotAvailabilityOverrideCreateRequest(
            city.Id, new DateOnly(2026, 12, 25), window.Id, null, null, "Staff unavailable"));

        created.IsSuccess.Should().BeTrue();
        created.Value.SlotWindowName.Should().Be("Morning");

        var listed = await service.ListOverridesAsync(city.Id, new DateOnly(2026, 12, 25));
        listed.Should().ContainSingle(o => o.Id == created.Value.Id);

        var deleted = await service.DeleteOverrideAsync(created.Value.Id);
        deleted.IsSuccess.Should().BeTrue();
        (await service.ListOverridesAsync(city.Id, new DateOnly(2026, 12, 25))).Should().BeEmpty();
    }

    /// <summary>
    /// Task 256: rendering one availability-override row resolved a city, a
    /// slot window, a category and a service - up to four queries - and
    /// ListOverridesAsync paid that per row. The four lookups are now batched
    /// across the whole page, so the command count must not grow with the
    /// number of overrides.
    /// </summary>
    [Fact]
    public async Task Listing_availability_overrides_does_not_scale_its_query_count_with_the_row_count()
    {
        var (_, city) = SeedCity("Pune");
        var date = new DateOnly(2026, 11, 14);
        var setupService = CreateService();

        for (int i = 0; i < 8; i++)
        {
            var window = (await setupService.CreateWindowAsync(new SlotWindowCreateRequest(
                city.Id, $"Window{i}", TimeSpan.FromHours(6 + i), TimeSpan.FromHours(7 + i), null, []))).Value;
            (await setupService.CreateOverrideAsync(new SlotAvailabilityOverrideCreateRequest(
                city.Id, date, window.Id, null, null, $"Reason {i}"))).IsSuccess.Should().BeTrue();
        }

        var counter = new CountingCommandInterceptor();
        var context = _db.CreateContext(counter);
        var service = new SlotManagementService(
            new SlotWindowRepository(context),
            new SlotBlackoutRepository(context),
            new SlotBookingPolicyRepository(context),
            new SlotAvailabilityOverrideRepository(context),
            new CityRepository(context),
            new CategoryRepository(context),
            new ServiceRepository(context));

        counter.Reset();
        var listed = await service.ListOverridesAsync(city.Id, date);

        listed.Should().HaveCount(8);
        listed.Should().OnlyContain(o => o.CityName == "Pune", "the batched city lookup must still resolve every row");
        listed.Should().OnlyContain(o => o.SlotWindowName != null, "the batched window lookup must still resolve every row");

        // The override page itself plus the four batched name lookups.
        counter.CommandCount.Should().BeLessThanOrEqualTo(5,
            "the four per-row name lookups must be batched across the page");
    }

    [Fact]
    public async Task Creating_an_override_for_a_nonexistent_window_returns_not_found()
    {
        var (_, city) = SeedCity("Surat");
        var service = CreateService();

        var result = await service.CreateOverrideAsync(new SlotAvailabilityOverrideCreateRequest(
            city.Id, new DateOnly(2026, 12, 25), Guid.NewGuid(), null, null, "Bad reference"));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Slots.WindowNotFound");
    }
}

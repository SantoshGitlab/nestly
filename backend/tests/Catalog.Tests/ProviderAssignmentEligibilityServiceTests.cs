using FluentAssertions;
using Nestly.Application;
using Nestly.Application.Bookings;
using Nestly.Application.ProviderManagement;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence.Repositories;
using Nestly.Infrastructure.Services;

namespace Nestly.Catalog.Tests;

/// <summary>Covers task 245: the per-candidate availability/blackout/capacity gate the automatic-assignment engine applies before assigning.</summary>
public sealed class ProviderAssignmentEligibilityServiceTests : IClassFixture<TestDatabase>
{
    private readonly TestDatabase _db;

    public ProviderAssignmentEligibilityServiceTests(TestDatabase db) => _db = db;

    private static readonly DateOnly SlotDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10));
    private static readonly TimeSpan SlotStart = TimeSpan.FromHours(9);
    private static readonly TimeSpan SlotEnd = TimeSpan.FromHours(13);

    private static Booking NewBooking(Guid customerId, Guid slotWindowId, DateOnly? date = null, TimeSpan? start = null, TimeSpan? end = null)
    {
        var address = new AddressSnapshot(
            "Home", "221B Baker Street", null, null, "560001", "Bengaluru", "Karnataka",
            12.9716m, 77.5946m, "Asha Rao", "9876543210");
        var slot = new SlotSnapshot(slotWindowId, date ?? SlotDate, "Morning", start ?? SlotStart, end ?? SlotEnd);
        var price = new PriceSnapshot(500m, 1, 500m, 0m, 50m, 550m, 18m, 99m, 10m, 659m);
        return new Booking(Guid.NewGuid(), customerId, new CustomerSnapshot("Asha Rao", "9876543210"), null, address, slot, price);
    }

    private ProviderAssignmentEligibilityService BuildService(Nestly.Infrastructure.Persistence.NestlyDbContext context) => new(
        new BookingRepository(context),
        new ProviderAvailabilityWindowRepository(context),
        new ProviderBlackoutDateRepository(context),
        new ProviderCapacityRepository(context),
        new ProviderScheduleConflictService(context),
        context);

    private sealed record Setup(Guid CustomerId, Guid ProviderId, Guid SlotWindowId, Guid BookingId);

    private async Task<Setup> SeedProviderWithAvailabilityAsync(bool matchingWindow = true)
    {
        using var context = _db.CreateContext();
        var customer = new Customer(Guid.NewGuid(), "9" + Guid.NewGuid().ToString("N")[..9], "Asha Rao", CustomerStatus.Active);
        var provider = new Provider(Guid.NewGuid(), "Ravi Kumar", "Ravi's Repairs", ProviderType.Individual, "+9198" + Guid.NewGuid().ToString("N")[..8]);
        provider.ChangeStatus(ProviderStatus.Active);
        context.Add(customer);
        context.Add(provider);

        if (matchingWindow)
        {
            context.Add(new ProviderAvailabilityWindow(Guid.NewGuid(), provider.Id, SlotDate.DayOfWeek, TimeSpan.FromHours(8), TimeSpan.FromHours(18)));
        }

        var slotWindowId = Guid.NewGuid();
        var booking = NewBooking(customer.Id, slotWindowId);
        await new BookingRepository(context).AddAsync(booking);
        context.SaveChanges();

        return new Setup(customer.Id, provider.Id, slotWindowId, booking.Id);
    }

    [Fact]
    public async Task IsEligibleAsync_true_when_availability_covers_the_slot_and_no_limits_are_set()
    {
        var setup = await SeedProviderWithAvailabilityAsync();

        using var readContext = _db.CreateContext();
        var eligible = await BuildService(readContext).IsEligibleAsync(setup.ProviderId, setup.BookingId);

        eligible.Should().BeTrue();
    }

    [Fact]
    public async Task IsEligibleAsync_false_when_the_provider_has_no_availability_windows_at_all()
    {
        var setup = await SeedProviderWithAvailabilityAsync(matchingWindow: false);

        using var readContext = _db.CreateContext();
        var eligible = await BuildService(readContext).IsEligibleAsync(setup.ProviderId, setup.BookingId);

        eligible.Should().BeFalse("no schedule on file is not the same as always available");
    }

    [Fact]
    public async Task IsEligibleAsync_false_when_the_window_is_on_a_different_day_of_week()
    {
        using var context = _db.CreateContext();
        var customer = new Customer(Guid.NewGuid(), "9" + Guid.NewGuid().ToString("N")[..9], "Asha Rao", CustomerStatus.Active);
        var provider = new Provider(Guid.NewGuid(), "Ravi Kumar", "Ravi's Repairs", ProviderType.Individual, "+9198" + Guid.NewGuid().ToString("N")[..8]);
        provider.ChangeStatus(ProviderStatus.Active);
        var wrongDay = (DayOfWeek)(((int)SlotDate.DayOfWeek + 1) % 7);
        context.Add(customer);
        context.Add(provider);
        context.Add(new ProviderAvailabilityWindow(Guid.NewGuid(), provider.Id, wrongDay, TimeSpan.FromHours(8), TimeSpan.FromHours(18)));
        var booking = NewBooking(customer.Id, Guid.NewGuid());
        await new BookingRepository(context).AddAsync(booking);
        context.SaveChanges();

        using var readContext = _db.CreateContext();
        var eligible = await BuildService(readContext).IsEligibleAsync(provider.Id, booking.Id);

        eligible.Should().BeFalse();
    }

    [Fact]
    public async Task IsEligibleAsync_false_when_the_window_does_not_cover_the_full_slot()
    {
        using var context = _db.CreateContext();
        var customer = new Customer(Guid.NewGuid(), "9" + Guid.NewGuid().ToString("N")[..9], "Asha Rao", CustomerStatus.Active);
        var provider = new Provider(Guid.NewGuid(), "Ravi Kumar", "Ravi's Repairs", ProviderType.Individual, "+9198" + Guid.NewGuid().ToString("N")[..8]);
        provider.ChangeStatus(ProviderStatus.Active);
        context.Add(customer);
        context.Add(provider);
        // Available 9am-11am, but the slot runs 9am-1pm - only a partial overlap.
        context.Add(new ProviderAvailabilityWindow(Guid.NewGuid(), provider.Id, SlotDate.DayOfWeek, TimeSpan.FromHours(9), TimeSpan.FromHours(11)));
        var booking = NewBooking(customer.Id, Guid.NewGuid());
        await new BookingRepository(context).AddAsync(booking);
        context.SaveChanges();

        using var readContext = _db.CreateContext();
        var eligible = await BuildService(readContext).IsEligibleAsync(provider.Id, booking.Id);

        eligible.Should().BeFalse();
    }

    [Fact]
    public async Task IsEligibleAsync_false_when_the_slot_date_falls_inside_a_blackout_range()
    {
        var setup = await SeedProviderWithAvailabilityAsync();
        using (var context = _db.CreateContext())
        {
            context.Add(new ProviderBlackoutDate(Guid.NewGuid(), setup.ProviderId, SlotDate.AddDays(-1), SlotDate.AddDays(1), "On leave"));
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        var eligible = await BuildService(readContext).IsEligibleAsync(setup.ProviderId, setup.BookingId);

        eligible.Should().BeFalse();
    }

    [Fact]
    public async Task IsEligibleAsync_false_once_MaxJobsPerDay_is_reached_by_other_live_assignments()
    {
        var setup = await SeedProviderWithAvailabilityAsync();
        using (var context = _db.CreateContext())
        {
            context.Add(new ProviderCapacity(Guid.NewGuid(), setup.ProviderId, maxJobsPerDay: 1));

            // A different booking, same date, already live-assigned to this
            // provider - deliberately at a non-overlapping later time (the
            // fixture booking runs 09:00-13:00), so what this test proves is
            // the per-day count and not task 288's overlap invariant, which
            // would otherwise reject an identical slot before the capacity
            // check ever ran.
            var otherBooking = NewBooking(setup.CustomerId, Guid.NewGuid(), start: TimeSpan.FromHours(14), end: TimeSpan.FromHours(16));
            await new BookingRepository(context).AddAsync(otherBooking);
            context.Add(new BookingProviderAssignment(Guid.NewGuid(), otherBooking.Id, setup.ProviderId, BookingAssignedByType.System, null, null));
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        var eligible = await BuildService(readContext).IsEligibleAsync(setup.ProviderId, setup.BookingId);

        eligible.Should().BeFalse();
    }

    [Fact]
    public async Task IsEligibleAsync_true_when_the_only_prior_assignment_for_the_day_was_rejected()
    {
        var setup = await SeedProviderWithAvailabilityAsync();
        using (var context = _db.CreateContext())
        {
            context.Add(new ProviderCapacity(Guid.NewGuid(), setup.ProviderId, maxJobsPerDay: 1));

            var otherBooking = NewBooking(setup.CustomerId, Guid.NewGuid());
            await new BookingRepository(context).AddAsync(otherBooking);
            var rejected = new BookingProviderAssignment(Guid.NewGuid(), otherBooking.Id, setup.ProviderId, BookingAssignedByType.System, null, null);
            rejected.Reject("Not available.");
            context.Add(rejected);
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        var eligible = await BuildService(readContext).IsEligibleAsync(setup.ProviderId, setup.BookingId);

        eligible.Should().BeTrue("a rejected assignment is no longer live and must not count against capacity");
    }

    /// <remarks>
    /// Since task 288 this outcome is over-determined: two bookings in the
    /// same window on the same date necessarily overlap in clock time, so the
    /// unconditional overlap check rejects them before MaxJobsPerSlot is even
    /// consulted. Kept as the documented behaviour of the limit; the overlap
    /// invariant itself is pinned by ProviderDoubleBookingTests.
    /// </remarks>
    [Fact]
    public async Task IsEligibleAsync_false_once_MaxJobsPerSlot_is_reached_even_with_MaxJobsPerDay_still_free()
    {
        var setup = await SeedProviderWithAvailabilityAsync();
        using (var context = _db.CreateContext())
        {
            context.Add(new ProviderCapacity(Guid.NewGuid(), setup.ProviderId, maxJobsPerDay: 10, maxJobsPerSlot: 1));

            // Same slot window and date as `setup.BookingId` - already live-assigned.
            var otherBooking = NewBooking(setup.CustomerId, setup.SlotWindowId);
            await new BookingRepository(context).AddAsync(otherBooking);
            context.Add(new BookingProviderAssignment(Guid.NewGuid(), otherBooking.Id, setup.ProviderId, BookingAssignedByType.System, null, null));
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        var eligible = await BuildService(readContext).IsEligibleAsync(setup.ProviderId, setup.BookingId);

        eligible.Should().BeFalse();
    }
}

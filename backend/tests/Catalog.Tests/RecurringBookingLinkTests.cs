using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Nestly.Application;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence;
using Nestly.Infrastructure.Persistence.Repositories;

namespace Nestly.Catalog.Tests;

/// <summary>
/// Task 296: the schema link between a <see cref="RecurringBookingPlan"/> and
/// the ordinary <see cref="Booking"/> rows it generates.
///
/// The plan-to-booking link is deliberately expressed TWICE, and these tests
/// pin both halves because tasks 297-300 build on them:
///
/// <list type="bullet">
/// <item><c>booking.recurring_booking_plan_id</c> - the forward link. This is
/// what makes "is this job recurring, and how often?" answerable from a
/// booking row the provider/admin list already loaded, with no join and no
/// second query (tasks 299, 300).</item>
/// <item><c>recurring_booking_occurrence</c> - the append-only audit row for
/// what the generator did on one scheduled date, INCLUDING the dates that
/// produced no booking at all. Those skipped rows are the reason it cannot be
/// replaced by the column above.</item>
/// </list>
///
/// Run against SQLite with <c>PRAGMA foreign_keys = ON</c> (see
/// <see cref="TestDatabase"/>), so the foreign keys and unique indexes
/// asserted here are the real ones the migration creates, not EF's in-memory
/// approximation of them.
/// </summary>
public sealed class RecurringBookingLinkTests : IClassFixture<TestDatabase>
{
    private readonly TestDatabase _db;

    public RecurringBookingLinkTests(TestDatabase db) => _db = db;

    private sealed record Fixture(Customer Customer, CustomerAddress Address, City City, Locality Locality, Service Service, SlotWindow Window);

    private static Fixture Seed(NestlyDbContext context)
    {
        var pincodeCode = Guid.NewGuid().ToString("N")[..6];
        var customer = new Customer(Guid.NewGuid(), "9" + Guid.NewGuid().ToString("N")[..9], "Priya Nair", CustomerStatus.Active);
        var address = new CustomerAddress(
            Guid.NewGuid(), customer.Id, "Home", "12 MG Road", null, null,
            pincodeCode, "Bengaluru", "Karnataka", 12.9716m, 77.5946m, "Priya Nair", "9876543210", true);
        var state = new State(Guid.NewGuid(), "Karnataka", "KA" + Guid.NewGuid().ToString("N")[..6]);
        var city = new City(Guid.NewGuid(), state.Id, "Bengaluru");
        var zone = new Zone(Guid.NewGuid(), city.Id, "Central");
        var pincode = new Pincode(Guid.NewGuid(), city.Id, pincodeCode);
        var locality = new Locality(Guid.NewGuid(), zone.Id, pincode.Id, "Koramangala");
        address.LinkToGeography(pincode.Id, locality.Id);
        var category = new Category(Guid.NewGuid(), "Cleaning", "cleaning-" + Guid.NewGuid(), "desc");
        var service = new Service(Guid.NewGuid(), category.Id, "Deep Clean", "deep-clean-" + Guid.NewGuid(), "desc", 500m);
        var window = new SlotWindow(Guid.NewGuid(), city.Id, "Morning", TimeSpan.FromHours(9), TimeSpan.FromHours(13));

        context.Add(customer);
        context.Add(address);
        context.States.Add(state);
        context.Cities.Add(city);
        context.Zones.Add(zone);
        context.Pincodes.Add(pincode);
        context.Localities.Add(locality);
        context.Add(category);
        context.Add(service);
        context.SlotWindows.Add(window);
        context.SaveChanges();

        return new Fixture(customer, address, city, locality, service, window);
    }

    private static RecurringBookingPlan NewPlan(Fixture fixture) =>
        new(Guid.NewGuid(), fixture.Customer.Id, fixture.Service.Id, fixture.City.Id, fixture.Locality.Id,
            fixture.Address.Id, fixture.Window.Id, quantity: 1,
            RecurringBookingRecurrenceFrequency.Weekly, DayOfWeek.Tuesday, recurrenceDayOfMonth: null,
            startDate: new DateOnly(2026, 8, 4), endDate: null, occurrenceCount: 4);

    private static Booking NewBooking(Fixture fixture, Guid? recurringBookingPlanId) =>
        new(Guid.NewGuid(),
            fixture.Customer.Id,
            new CustomerSnapshot(fixture.Customer.Name, fixture.Customer.Mobile),
            fixture.Address.Id,
            new AddressSnapshot("Home", "12 MG Road", null, null, "560001", "Bengaluru", "Karnataka", 12.9716m, 77.5946m, "Priya Nair", "9876543210"),
            new SlotSnapshot(fixture.Window.Id, new DateOnly(2026, 8, 4), "Morning", TimeSpan.FromHours(9), TimeSpan.FromHours(13)),
            new PriceSnapshot(500m, 1, 500m, 0m, 50m, 550m, 18m, 99m, 10m, 659m),
            recurringBookingPlanId: recurringBookingPlanId);

    [Fact]
    public async Task A_generated_occurrence_is_an_ordinary_booking_row_carrying_its_plan_id()
    {
        Guid planId;
        Guid bookingId;

        using (var context = _db.CreateContext())
        {
            var fixture = Seed(context);
            var plan = NewPlan(fixture);
            context.RecurringBookingPlans.Add(plan);
            await context.SaveChangesAsync();

            var booking = NewBooking(fixture, plan.Id);
            context.Bookings.Add(booking);
            await context.SaveChangesAsync();

            planId = plan.Id;
            bookingId = booking.Id;
        }

        using (var context = _db.CreateContext())
        {
            var reloaded = await context.Bookings.AsNoTracking().SingleAsync(b => b.Id == bookingId);

            // The whole point of the row's "reuses the existing Booking entity"
            // requirement: it is a normal booking in every other respect.
            reloaded.RecurringBookingPlanId.Should().Be(planId);
            reloaded.Status.Should().Be(BookingStatus.Initiated);
            reloaded.TotalPayableSnapshot.Should().Be(659m);
        }
    }

    [Fact]
    public async Task An_ordinary_one_off_booking_has_no_plan_id()
    {
        Guid bookingId;

        using (var context = _db.CreateContext())
        {
            var fixture = Seed(context);
            var booking = NewBooking(fixture, recurringBookingPlanId: null);
            context.Bookings.Add(booking);
            await context.SaveChangesAsync();
            bookingId = booking.Id;
        }

        using var verify = _db.CreateContext();
        var reloaded = await verify.Bookings.AsNoTracking().SingleAsync(b => b.Id == bookingId);

        reloaded.RecurringBookingPlanId.Should().BeNull();
    }

    [Fact]
    public async Task A_booking_cannot_point_at_a_plan_that_does_not_exist()
    {
        // The forward link is a real foreign key, not a loose traceability
        // column - a dangling plan id would silently break tasks 299/300's
        // "which plan is this job from" read.
        using var context = _db.CreateContext();
        var fixture = Seed(context);
        context.Bookings.Add(NewBooking(fixture, recurringBookingPlanId: Guid.NewGuid()));

        var act = () => context.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task ListByRecurringPlanAsync_returns_only_that_plans_bookings()
    {
        using var context = _db.CreateContext();
        var fixture = Seed(context);

        var plan = NewPlan(fixture);
        var otherPlan = NewPlan(fixture);
        context.RecurringBookingPlans.AddRange(plan, otherPlan);
        await context.SaveChangesAsync();

        var mine = NewBooking(fixture, plan.Id);
        context.Bookings.Add(mine);
        context.Bookings.Add(NewBooking(fixture, otherPlan.Id));
        context.Bookings.Add(NewBooking(fixture, recurringBookingPlanId: null));
        await context.SaveChangesAsync();

        var result = await new BookingRepository(context).ListByRecurringPlanAsync(plan.Id);

        result.Should().ContainSingle();
        result[0].Id.Should().Be(mine.Id);
    }

    [Fact]
    public async Task The_occurrence_log_keeps_skipped_dates_that_produced_no_booking()
    {
        // This is why the occurrence table is not replaceable by the column on
        // booking: there is no booking row for a skipped date to hang off.
        using var context = _db.CreateContext();
        var fixture = Seed(context);
        var plan = NewPlan(fixture);
        context.RecurringBookingPlans.Add(plan);
        await context.SaveChangesAsync();

        var booking = NewBooking(fixture, plan.Id);
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        context.RecurringBookingOccurrences.Add(new RecurringBookingOccurrence(
            Guid.NewGuid(), plan.Id, new DateOnly(2026, 8, 4), RecurringBookingOccurrenceOutcome.Booked, booking.Id, null));
        context.RecurringBookingOccurrences.Add(new RecurringBookingOccurrence(
            Guid.NewGuid(), plan.Id, new DateOnly(2026, 8, 11), RecurringBookingOccurrenceOutcome.SkippedSlotUnavailable, null, "Slot no longer available."));
        await context.SaveChangesAsync();

        var occurrences = await new RecurringBookingOccurrenceRepository(context).ListByPlanAsync(plan.Id);

        occurrences.Should().HaveCount(2);
        occurrences.Should().ContainSingle(o => o.Outcome == RecurringBookingOccurrenceOutcome.Booked && o.BookingId == booking.Id);
        occurrences.Should().ContainSingle(o => o.BookingId == null && o.SkipReason == "Slot no longer available.");
    }

    [Fact]
    public async Task Two_occurrences_cannot_claim_the_same_generated_booking()
    {
        using var context = _db.CreateContext();
        var fixture = Seed(context);
        var plan = NewPlan(fixture);
        context.RecurringBookingPlans.Add(plan);
        await context.SaveChangesAsync();

        var booking = NewBooking(fixture, plan.Id);
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        context.RecurringBookingOccurrences.Add(new RecurringBookingOccurrence(
            Guid.NewGuid(), plan.Id, new DateOnly(2026, 8, 4), RecurringBookingOccurrenceOutcome.Booked, booking.Id, null));
        await context.SaveChangesAsync();

        // A different scheduled date, so the (plan, date) idempotency index
        // does not catch this - only the unique index on booking_id does.
        context.RecurringBookingOccurrences.Add(new RecurringBookingOccurrence(
            Guid.NewGuid(), plan.Id, new DateOnly(2026, 8, 11), RecurringBookingOccurrenceOutcome.Booked, booking.Id, null));

        var act = () => context.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }
}

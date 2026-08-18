using FluentAssertions;
using Nestly.Application;
using Nestly.Application.ProviderManagement;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence;
using Nestly.Infrastructure.Services;

namespace Nestly.Catalog.Tests;

/// <summary>
/// Task 321: detection of provider double-bookings that already exist.
///
/// <para>
/// <see cref="ProviderScheduleConflictService"/> (task 288) is a gate - it
/// answers "may this provider take this booking?" on the way in. These tests
/// cover the reporting counterpart, which answers "who is double-booked right
/// now?" for rows that predate that gate, lost a race with it, or were written
/// by something that bypassed it. The two must agree on what a conflict is,
/// so the overlap cases below deliberately mirror
/// <c>ProviderDoubleBookingTests</c>: half-open <c>[start, end)</c>, and live
/// meaning Assigned/Accepted only.
/// </para>
///
/// <para>
/// Conflicts are seeded directly through the context rather than through
/// <c>BookingProviderAssignmentService</c>, because that service correctly
/// refuses to create them. That is the point: the state under test is one the
/// application's own write path cannot produce, which is exactly why a
/// detector is needed at all.
/// </para>
/// </summary>
public sealed class BookingAssignmentConflictServiceTests : IClassFixture<TestDatabase>
{
    private readonly TestDatabase _db;

    public BookingAssignmentConflictServiceTests(TestDatabase db) => _db = db;

    private static readonly DateOnly SlotDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10));

    /// <summary>
    /// A date isolated to one test. TestDatabase is an <see cref="IClassFixture{TFixture}"/>
    /// - one persistent in-memory SQLite database shared by every test method
    /// in this class, by design (see TestDatabase's own doc comment). That is
    /// fine for tests that query a single provider/booking by id, but
    /// BookingAssignmentConflictService.SearchAsync scans every live
    /// assignment in a date RANGE regardless of provider - so two tests
    /// sharing a date would see each other's conflicts. Each test below gets
    /// its own multi-day block, far enough apart that no test's date range
    /// (some span several days) can ever reach another's.
    /// </summary>
    private static DateOnly TestDate(int block) => SlotDate.AddDays(block * 10);

    private static readonly TimeSpan NineAm = TimeSpan.FromHours(9);
    private static readonly TimeSpan TenAm = TimeSpan.FromHours(10);
    private static readonly TimeSpan ElevenAm = TimeSpan.FromHours(11);
    private static readonly TimeSpan Noon = TimeSpan.FromHours(12);
    private static readonly TimeSpan OnePm = TimeSpan.FromHours(13);

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

    private static Booking AddBooking(NestlyDbContext context, Fixture f, TimeSpan start, TimeSpan end, DateOnly? date = null)
    {
        var windowId = Guid.NewGuid();
        context.SlotWindows.Add(new SlotWindow(windowId, f.CityId, "Window", start, end));

        var address = new AddressSnapshot(
            "Home", "221B Baker Street", null, null, f.PincodeCode, "Bengaluru", "Karnataka",
            12.9352m, 77.6245m, "Asha Rao", "9876543210");
        var slot = new SlotSnapshot(windowId, date ?? SlotDate, "Window", start, end);
        var price = new PriceSnapshot(500m, 1, 500m, 0m, 50m, 550m, 18m, 99m, 10m, 659m);

        var booking = new Booking(Guid.NewGuid(), f.CustomerId, new CustomerSnapshot("Asha Rao", "9876543210"), null, address, slot, price);
        booking.AddItem(Guid.NewGuid(), f.ServiceId, "Deep Clean", "deep-clean", 500m, 1);
        booking.TransitionTo(BookingStatus.PaymentPending);
        booking.TransitionTo(BookingStatus.Confirmed);
        booking.TransitionTo(BookingStatus.AwaitingFulfilment);
        context.Add(booking);
        context.SaveChanges();
        return booking;
    }

    private static Provider AddProvider(NestlyDbContext context, string displayName = "Ravi's Repairs")
    {
        var provider = new Provider(Guid.NewGuid(), "Ravi Kumar", displayName, ProviderType.Individual, "+9198" + Guid.NewGuid().ToString("N")[..8]);
        provider.ChangeStatus(ProviderStatus.Active);
        context.Add(provider);
        context.SaveChanges();
        return provider;
    }

    /// <summary>
    /// Writes the assignment row directly - see the class remarks on why this
    /// cannot go through BookingProviderAssignmentService.
    /// </summary>
    private static void AddLiveAssignment(
        NestlyDbContext context,
        Booking booking,
        Provider provider,
        BookingProviderAssignmentStatus status = BookingProviderAssignmentStatus.Assigned)
    {
        var assignment = new BookingProviderAssignment(
            Guid.NewGuid(), booking.Id, provider.Id, BookingAssignedByType.Admin, Guid.NewGuid(), null);

        if (status == BookingProviderAssignmentStatus.Accepted)
        {
            assignment.Accept();
        }
        else if (status != BookingProviderAssignmentStatus.Assigned)
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "Only live statuses are seedable here.");
        }

        context.Add(assignment);
        context.SaveChanges();
    }

    private static BookingAssignmentConflictService Build(NestlyDbContext context) => new(context);

    [Fact]
    public async Task Reports_two_overlapping_live_jobs_on_one_provider_as_a_single_group()
    {
        var date = TestDate(0);
        using var context = _db.CreateContext();
        var f = Seed(context);
        var provider = AddProvider(context);

        var first = AddBooking(context, f, NineAm, ElevenAm, date);
        var second = AddBooking(context, f, TenAm, Noon, date);
        AddLiveAssignment(context, first, provider);
        AddLiveAssignment(context, second, provider, BookingProviderAssignmentStatus.Accepted);

        var result = await Build(context).SearchAsync(date, date, page: 1, pageSize: 20);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(1);

        var group = result.Value.Items.Single();
        group.ProviderId.Should().Be(provider.Id);
        group.ProviderDisplayName.Should().Be("Ravi's Repairs");
        group.SlotDate.Should().Be(date);
        // The span the clash lives inside, not either booking's own slot.
        group.WindowStart.Should().Be(NineAm);
        group.WindowEnd.Should().Be(Noon);
        group.Bookings.Select(b => b.BookingId).Should().BeEquivalentTo([first.Id, second.Id]);
        group.Bookings.Should().Contain(b => b.AssignmentStatus == BookingProviderAssignmentStatus.Accepted);
        group.Bookings.Should().OnlyContain(b => b.ServiceName == "Deep Clean");
    }

    [Fact]
    public async Task Back_to_back_jobs_are_not_a_conflict()
    {
        var date = TestDate(1);
        using var context = _db.CreateContext();
        var f = Seed(context);
        var provider = AddProvider(context);

        // [09:00, 11:00) then [11:00, 13:00): they touch at an endpoint and
        // share no interior instant, so both stay legal - the same half-open
        // rule the assignment gate and the DB constraint apply.
        AddLiveAssignment(context, AddBooking(context, f, NineAm, ElevenAm, date), provider);
        AddLiveAssignment(context, AddBooking(context, f, ElevenAm, OnePm, date), provider);

        var result = await Build(context).SearchAsync(date, date, page: 1, pageSize: 20);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Overlapping_jobs_on_different_providers_are_not_a_conflict()
    {
        var date = TestDate(2);
        using var context = _db.CreateContext();
        var f = Seed(context);

        AddLiveAssignment(context, AddBooking(context, f, NineAm, ElevenAm, date), AddProvider(context, "Alpha Services"));
        AddLiveAssignment(context, AddBooking(context, f, NineAm, ElevenAm, date), AddProvider(context, "Beta Services"));

        var result = await Build(context).SearchAsync(date, date, page: 1, pageSize: 20);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Same_times_on_different_dates_are_not_a_conflict()
    {
        var date = TestDate(3);
        using var context = _db.CreateContext();
        var f = Seed(context);
        var provider = AddProvider(context);

        AddLiveAssignment(context, AddBooking(context, f, NineAm, ElevenAm, date), provider);
        AddLiveAssignment(context, AddBooking(context, f, NineAm, ElevenAm, date.AddDays(1)), provider);

        var result = await Build(context).SearchAsync(date, date.AddDays(2), page: 1, pageSize: 20);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task A_superseded_assignment_does_not_conflict_with_the_live_one()
    {
        var date = TestDate(4);
        using var context = _db.CreateContext();
        var f = Seed(context);
        var provider = AddProvider(context);

        var stale = new BookingProviderAssignment(
            Guid.NewGuid(), AddBooking(context, f, NineAm, ElevenAm, date).Id, provider.Id,
            BookingAssignedByType.Admin, Guid.NewGuid(), null);
        stale.MarkReassigned(Guid.NewGuid());
        context.Add(stale);
        context.SaveChanges();

        AddLiveAssignment(context, AddBooking(context, f, TenAm, Noon, date), provider);

        var result = await Build(context).SearchAsync(date, date, page: 1, pageSize: 20);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Three_mutually_overlapping_jobs_form_one_group_not_three_pairs()
    {
        var date = TestDate(5);
        using var context = _db.CreateContext();
        var f = Seed(context);
        var provider = AddProvider(context);

        // A long job containing two shorter ones: the walk must track the
        // furthest end seen, not the previous job's end, or the third booking
        // would start a second group.
        AddLiveAssignment(context, AddBooking(context, f, NineAm, OnePm, date), provider);
        AddLiveAssignment(context, AddBooking(context, f, TenAm, ElevenAm, date), provider);
        AddLiveAssignment(context, AddBooking(context, f, Noon, OnePm, date), provider);

        var result = await Build(context).SearchAsync(date, date, page: 1, pageSize: 20);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(1);
        result.Value.Items.Single().Bookings.Should().HaveCount(3);
    }

    [Fact]
    public async Task Count_matches_the_number_of_groups_from_the_given_date()
    {
        var date = TestDate(6);
        using var context = _db.CreateContext();
        var f = Seed(context);
        var provider = AddProvider(context);

        AddLiveAssignment(context, AddBooking(context, f, NineAm, ElevenAm, date), provider);
        AddLiveAssignment(context, AddBooking(context, f, TenAm, Noon, date), provider);

        var result = await Build(context).CountAsync(date);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(1);
    }

    [Fact]
    public async Task Rejects_a_page_size_above_the_cap()
    {
        using var context = _db.CreateContext();

        var result = await Build(context).SearchAsync(SlotDate, SlotDate, page: 1, pageSize: 500);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("BookingConflicts.InvalidPageSize");
    }

    [Fact]
    public async Task Rejects_a_reversed_date_range()
    {
        using var context = _db.CreateContext();

        var result = await Build(context).SearchAsync(SlotDate, SlotDate.AddDays(-1), page: 1, pageSize: 20);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("BookingConflicts.InvalidRange");
    }
}

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nestly.Application;
using Nestly.Application.Bookings;
using Nestly.Application.ProviderManagement;
using Nestly.Domain;
using Nestly.Domain.Events;
using Nestly.Infrastructure.Options;
using Nestly.Infrastructure.Persistence.Interceptors;
using Nestly.Infrastructure.Persistence.Repositories;
using Nestly.Infrastructure.Services;

namespace Nestly.Catalog.Tests;

/// <summary>Covers task 246: automatic provider assignment on a booking reaching AwaitingFulfilment.</summary>
public sealed class ProviderAutoAssignmentHandlerTests : IClassFixture<TestDatabase>
{
    private readonly TestDatabase _db;

    public ProviderAutoAssignmentHandlerTests(TestDatabase db) => _db = db;

    private static readonly DateOnly SlotDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10));

    private sealed record Fixture(Guid CustomerId, Guid CategoryId, Guid ServiceId, Guid CityId, Guid SlotWindowId, Guid BookingId);

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

        var address = new AddressSnapshot(
            "Home", "221B Baker Street", null, null, pincodeCode, "Bengaluru", "Karnataka",
            12.9352m, 77.6245m, "Asha Rao", "9876543210");
        var slot = new SlotSnapshot(window.Id, SlotDate, "Morning", TimeSpan.FromHours(9), TimeSpan.FromHours(13));
        var price = new PriceSnapshot(500m, 1, 500m, 0m, 50m, 550m, 18m, 99m, 10m, 659m);
        var booking = new Booking(Guid.NewGuid(), customer.Id, new CustomerSnapshot(customer.Name, customer.Mobile), null, address, slot, price);
        booking.AddItem(Guid.NewGuid(), service.Id, service.Name, service.Slug, 500m, 1);
        booking.TransitionTo(BookingStatus.PaymentPending);
        booking.TransitionTo(BookingStatus.Confirmed);
        booking.TransitionTo(BookingStatus.AwaitingFulfilment);
        context.Add(booking);
        context.SaveChanges();

        return new Fixture(customer.Id, category.Id, service.Id, city.Id, window.Id, booking.Id);
    }

    private static Provider AddActiveEligibleProvider(Nestly.Infrastructure.Persistence.NestlyDbContext context, Fixture f, decimal lat, decimal lng)
    {
        var provider = new Provider(Guid.NewGuid(), "Ravi Kumar", "Ravi's Repairs", ProviderType.Individual, "+9198" + Guid.NewGuid().ToString("N")[..8]);
        provider.ChangeStatus(ProviderStatus.Active);
        provider.UpdateLocation(lat, lng);
        context.Add(provider);
        context.Add(new ProviderSkillMapping(Guid.NewGuid(), provider.Id, f.CategoryId));
        context.Add(new ProviderServiceArea(Guid.NewGuid(), provider.Id, f.CityId));
        context.Add(new ProviderAvailabilityWindow(Guid.NewGuid(), provider.Id, SlotDate.DayOfWeek, TimeSpan.FromHours(8), TimeSpan.FromHours(18)));
        return provider;
    }

    // The real SandboxRouteEstimateProvider, not a stub: it needs no HTTP and
    // no key, and task 267 defines a sandbox-only response as carrying no road
    // information, so candidate ranking here stays straight-line - exactly the
    // behaviour these tests were written against. Task 267's reordering is
    // covered in ProviderMatchingServiceRouteRankingTests.
    private static ProviderAutoAssignmentHandler BuildHandler(Nestly.Infrastructure.Persistence.NestlyDbContext context, int retryAttempts = 3, bool enabled = true) => new(
        new ProviderMatchingService(
            new BookingRepository(context),
            context,
            new SandboxRouteEstimateProvider(Options.Create(new SandboxRouteEstimateOptions())),
            Options.Create(new AutoAssignmentOptions())),
        new ProviderAssignmentEligibilityService(
            new BookingRepository(context),
            new ProviderAvailabilityWindowRepository(context),
            new ProviderBlackoutDateRepository(context),
            new ProviderCapacityRepository(context),
            new ProviderScheduleConflictService(context),
            TravelFeasibilityFactory.Sandbox(context),
            context),
        new BookingProviderAssignmentService(new BookingRepository(context), new ProviderRepository(context), new ServiceRepository(context), new BookingProviderAssignmentRepository(context), new ProviderScheduleConflictService(context), context),
        new BookingProviderAssignmentRepository(context),
        Options.Create(new AutoAssignmentOptions { RetryAttempts = retryAttempts, Enabled = enabled }),
        NullLogger<ProviderAutoAssignmentHandler>.Instance);

    private static DomainEventNotification<BookingStatusChangedEvent> AwaitingFulfilmentEvent(Guid bookingId) =>
        new(new BookingStatusChangedEvent(bookingId, BookingStatus.Confirmed, BookingStatus.AwaitingFulfilment));

    [Fact]
    public async Task Handle_assigns_the_nearest_eligible_provider_when_a_booking_reaches_AwaitingFulfilment()
    {
        Fixture f;
        Provider near, far;
        using (var context = _db.CreateContext())
        {
            f = Seed(context);
            far = AddActiveEligibleProvider(context, f, 13.0827m, 80.2707m);
            near = AddActiveEligibleProvider(context, f, 12.9352m, 77.6146m);
            context.SaveChanges();
        }

        using (var context = _db.CreateContext())
        {
            await BuildHandler(context).Handle(AwaitingFulfilmentEvent(f.BookingId), CancellationToken.None);
        }

        using var readContext = _db.CreateContext();
        var booking = await new BookingRepository(readContext).GetByIdAsync(f.BookingId);
        booking!.Status.Should().Be(BookingStatus.Assigned);
        booking.AssignedProviderId.Should().Be(near.Id, "the nearer of the two eligible candidates must win");

        var assignment = await new BookingProviderAssignmentRepository(readContext).GetActiveByBookingAsync(f.BookingId);
        assignment.Should().NotBeNull();
        assignment!.AssignedByType.Should().Be(BookingAssignedByType.System);
        assignment.AssignedByUserId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ignores_transitions_other_than_AwaitingFulfilment()
    {
        Fixture f;
        using (var context = _db.CreateContext())
        {
            f = Seed(context);
            AddActiveEligibleProvider(context, f, 12.9352m, 77.6146m);
            context.SaveChanges();
        }

        using (var context = _db.CreateContext())
        {
            var evt = new DomainEventNotification<BookingStatusChangedEvent>(
                new BookingStatusChangedEvent(f.BookingId, BookingStatus.PaymentPending, BookingStatus.Confirmed));
            await BuildHandler(context).Handle(evt, CancellationToken.None);
        }

        using var readContext = _db.CreateContext();
        var booking = await new BookingRepository(readContext).GetByIdAsync(f.BookingId);
        booking!.Status.Should().Be(BookingStatus.AwaitingFulfilment, "the fixture booking starts here and this event's ToStatus isn't AwaitingFulfilment, so the handler must not touch it");
        booking.AssignedProviderId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_leaves_the_booking_in_AwaitingFulfilment_when_no_candidate_is_eligible()
    {
        Fixture f;
        using (var context = _db.CreateContext())
        {
            f = Seed(context);
            // No provider seeded at all.
        }

        using (var context = _db.CreateContext())
        {
            await BuildHandler(context).Handle(AwaitingFulfilmentEvent(f.BookingId), CancellationToken.None);
        }

        using var readContext = _db.CreateContext();
        var booking = await new BookingRepository(readContext).GetByIdAsync(f.BookingId);
        booking!.Status.Should().Be(BookingStatus.AwaitingFulfilment, "no eligible provider is not an error - the booking stays exactly where today's manual admin queue already picks it up");
        booking.AssignedProviderId.Should().BeNull();
    }

    [Fact]
    public async Task TryAssignAsync_skips_excluded_providers()
    {
        Fixture f;
        Provider excluded, other;
        using (var context = _db.CreateContext())
        {
            f = Seed(context);
            excluded = AddActiveEligibleProvider(context, f, 12.9352m, 77.6146m);
            other = AddActiveEligibleProvider(context, f, 13.0827m, 80.2707m);
            context.SaveChanges();
        }

        using (var context = _db.CreateContext())
        {
            var assigned = await BuildHandler(context).TryAssignAsync(f.BookingId, [excluded.Id]);
            assigned.Should().BeTrue();
        }

        using var readContext = _db.CreateContext();
        var booking = await new BookingRepository(readContext).GetByIdAsync(f.BookingId);
        booking!.AssignedProviderId.Should().Be(other.Id);
    }

    /// <summary>Task 247's actual end-to-end path: RejectAsync's own TransitionTo re-raises AwaitingFulfilment (no new coupling needed between BookingProviderAssignmentService and this handler), and this handler's Handle() must read that history back and exclude the rejecter.</summary>
    [Fact]
    public async Task Handle_excludes_a_provider_who_already_rejected_this_booking_and_assigns_the_next_one()
    {
        Fixture f;
        Provider rejecter, other;
        using (var context = _db.CreateContext())
        {
            f = Seed(context);
            rejecter = AddActiveEligibleProvider(context, f, 12.9352m, 77.6146m);
            other = AddActiveEligibleProvider(context, f, 13.0827m, 80.2707m);
            context.SaveChanges();
        }

        using (var context = _db.CreateContext())
        {
            var assignmentService = new BookingProviderAssignmentService(
                new BookingRepository(context), new ProviderRepository(context), new ServiceRepository(context), new BookingProviderAssignmentRepository(context), new ProviderScheduleConflictService(context), context);

            var firstAssign = await assignmentService.AssignBySystemAsync(f.BookingId, rejecter.Id);
            firstAssign.IsSuccess.Should().BeTrue();

            var rejectResult = await assignmentService.RejectByProviderAsync(f.BookingId, rejecter.Id, new RejectAssignmentRequest("Too far"));
            rejectResult.IsSuccess.Should().BeTrue();
        }

        using (var context = _db.CreateContext())
        {
            await BuildHandler(context).Handle(AwaitingFulfilmentEvent(f.BookingId), CancellationToken.None);
        }

        using var readContext = _db.CreateContext();
        var booking = await new BookingRepository(readContext).GetByIdAsync(f.BookingId);
        booking!.AssignedProviderId.Should().Be(other.Id, "the provider who already rejected must be excluded from the retry");

        var history = await new BookingProviderAssignmentRepository(readContext).ListByBookingAsync(f.BookingId);
        history.Should().Contain(a => a.ProviderId == rejecter.Id && a.Status == BookingProviderAssignmentStatus.Rejected);
    }

    [Fact]
    public async Task Handle_stops_retrying_once_the_configured_cap_is_reached_even_with_an_eligible_candidate_left()
    {
        Fixture f;
        Provider stillEligible;
        using (var context = _db.CreateContext())
        {
            f = Seed(context);
            // Two providers already auto-assigned-then-rejected (retry cap = 2 below), plus one untouched, still-eligible provider.
            var rejectedOne = AddActiveEligibleProvider(context, f, 12.9352m, 77.6146m);
            var rejectedTwo = AddActiveEligibleProvider(context, f, 12.94m, 77.62m);
            stillEligible = AddActiveEligibleProvider(context, f, 13.0827m, 80.2707m);
            context.Add(new BookingProviderAssignment(Guid.NewGuid(), f.BookingId, rejectedOne.Id, BookingAssignedByType.System, null, null));
            context.Add(new BookingProviderAssignment(Guid.NewGuid(), f.BookingId, rejectedTwo.Id, BookingAssignedByType.System, null, null));
            var rows = context.Set<BookingProviderAssignment>().Local.ToList();
            foreach (var row in rows)
            {
                row.Reject("Too far");
            }
            context.SaveChanges();
        }

        using (var context = _db.CreateContext())
        {
            await BuildHandler(context, retryAttempts: 2).Handle(AwaitingFulfilmentEvent(f.BookingId), CancellationToken.None);
        }

        using var readContext = _db.CreateContext();
        var booking = await new BookingRepository(readContext).GetByIdAsync(f.BookingId);
        booking!.Status.Should().Be(BookingStatus.AwaitingFulfilment, "the retry cap was already reached, so even a fresh eligible candidate must not be auto-assigned");
        booking.AssignedProviderId.Should().BeNull();
        _ = stillEligible; // never assigned - proven by the assertions above.
    }

    /// <summary>Task 248: the kill switch must produce zero behaviour change from before this whole phase existed - not a new error path, just untouched.</summary>
    [Fact]
    public async Task Handle_does_nothing_when_AutoAssignmentOptions_Enabled_is_false()
    {
        Fixture f;
        using (var context = _db.CreateContext())
        {
            f = Seed(context);
            AddActiveEligibleProvider(context, f, 12.9352m, 77.6146m);
            context.SaveChanges();
        }

        using (var context = _db.CreateContext())
        {
            await BuildHandler(context, enabled: false).Handle(AwaitingFulfilmentEvent(f.BookingId), CancellationToken.None);
        }

        using var readContext = _db.CreateContext();
        var booking = await new BookingRepository(readContext).GetByIdAsync(f.BookingId);
        booking!.Status.Should().Be(BookingStatus.AwaitingFulfilment);
        booking.AssignedProviderId.Should().BeNull();
    }
}

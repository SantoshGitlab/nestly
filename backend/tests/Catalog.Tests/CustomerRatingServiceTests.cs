using FluentAssertions;
using Microsoft.Extensions.Options;
using Nestly.Application;
using Nestly.Application.CustomerRatings;
using Nestly.Domain;
using Nestly.Infrastructure.Options;
using Nestly.Infrastructure.Persistence.Repositories;
using Nestly.Infrastructure.Services;

namespace Nestly.Catalog.Tests;

/// <summary>Covers bidirectional reviews' provider-side rating: completed-job eligibility, one rating per booking, only the assigned provider may rate, optional time window - the reverse-direction analogue of <see cref="ReviewServiceTests"/>.</summary>
public sealed class CustomerRatingServiceTests : IClassFixture<TestDatabase>
{
    private readonly TestDatabase _db;

    public CustomerRatingServiceTests(TestDatabase db) => _db = db;

    private sealed record Fixture(Customer Customer, Provider Provider, Guid BookingId);

    private Fixture SeedBooking(Nestly.Infrastructure.Persistence.NestlyDbContext context, BookingStatus finalStatus, DateTime completedAt, bool assignProvider = true)
    {
        var customer = new Customer(Guid.NewGuid(), "9" + Guid.NewGuid().ToString("N")[..9], "Asha Rao", CustomerStatus.Active);
        var provider = new Provider(Guid.NewGuid(), "Ravi Kumar", "Ravi K", ProviderType.Individual, "9" + Guid.NewGuid().ToString("N")[..9]);
        var category = new Category(Guid.NewGuid(), "Cleaning", "cleaning-" + Guid.NewGuid(), "desc");
        var service = new Service(Guid.NewGuid(), category.Id, "Deep Clean", "deep-clean-" + Guid.NewGuid(), "desc", 999m);

        var booking = new Booking(
            Guid.NewGuid(), customer.Id,
            new CustomerSnapshot(customer.Name, customer.Mobile),
            null,
            new AddressSnapshot("Home", "221B Baker Street", null, null, "560001", "Bengaluru", "Karnataka", 12.9716m, 77.5946m, "Asha Rao", "9876543210"),
            new SlotSnapshot(Guid.NewGuid(), DateOnly.FromDateTime(completedAt), "Morning", TimeSpan.FromHours(9), TimeSpan.FromHours(13)),
            new PriceSnapshot(999m, 1, 999m, 0m, 0m, 999m, 0m, 0m, 0m, 999m));
        booking.AddItem(Guid.NewGuid(), service.Id, service.Name, service.Slug, 999m, 1);

        booking.TransitionTo(BookingStatus.PaymentPending);
        booking.TransitionTo(BookingStatus.Confirmed);

        if (assignProvider)
        {
            booking.AssignProvider(provider.Id);
        }

        if (finalStatus == BookingStatus.Completed)
        {
            booking.TransitionTo(BookingStatus.AwaitingFulfilment);
            booking.TransitionTo(BookingStatus.Assigned);
            booking.TransitionTo(BookingStatus.InProgress);
            booking.TransitionTo(BookingStatus.Completed);
        }

        context.Add(customer);
        context.Add(provider);
        context.Add(category);
        context.Add(service);
        context.Add(booking);
        context.SaveChanges();

        if (finalStatus == BookingStatus.Completed)
        {
            var completedRow = context.Set<BookingStatusHistory>().Single(h => h.BookingId == booking.Id && h.ToStatus == BookingStatus.Completed);
            typeof(BookingStatusHistory).GetProperty(nameof(BookingStatusHistory.ChangedAtUtc))!.SetValue(completedRow, completedAt);
            context.SaveChanges();
        }

        return new Fixture(customer, provider, booking.Id);
    }

    private static CustomerRatingService BuildService(Nestly.Infrastructure.Persistence.NestlyDbContext context, DateTime now, ReviewPolicyOptions? policy = null) =>
        new(new BookingRepository(context), new CustomerRatingRepository(context), new FakeTimeProvider(now), Options.Create(policy ?? new ReviewPolicyOptions()));

    [Fact]
    public async Task Only_a_completed_job_is_eligible_for_rating()
    {
        Fixture fixture;
        using (var context = _db.CreateContext())
        {
            fixture = SeedBooking(context, BookingStatus.Confirmed, DateTime.UtcNow);
        }

        using var readContext = _db.CreateContext();
        var result = await BuildService(readContext, DateTime.UtcNow).GetEligibilityAsync(fixture.Provider.Id, fixture.BookingId);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsEligible.Should().BeFalse();
        result.Value.IneligibilityReason.Should().Contain("completed");
    }

    [Fact]
    public async Task SubmitAsync_succeeds_for_a_completed_job_within_the_window()
    {
        Fixture fixture;
        using (var context = _db.CreateContext())
        {
            fixture = SeedBooking(context, BookingStatus.Completed, DateTime.UtcNow.AddDays(-5));
        }

        using var context2 = _db.CreateContext();
        var result = await BuildService(context2, DateTime.UtcNow).SubmitAsync(
            fixture.Provider.Id, fixture.BookingId, new SubmitCustomerRatingRequest(4, "Great to work with"));

        result.IsSuccess.Should().BeTrue();
        result.Value.Rating.Should().Be(4);
        result.Value.CustomerId.Should().Be(fixture.Customer.Id);
    }

    [Fact]
    public async Task SubmitAsync_rejects_a_second_rating_for_the_same_booking()
    {
        Fixture fixture;
        using (var context = _db.CreateContext())
        {
            fixture = SeedBooking(context, BookingStatus.Completed, DateTime.UtcNow.AddDays(-1));
        }

        using (var firstContext = _db.CreateContext())
        {
            var first = await BuildService(firstContext, DateTime.UtcNow).SubmitAsync(fixture.Provider.Id, fixture.BookingId, new SubmitCustomerRatingRequest(5, null));
            first.IsSuccess.Should().BeTrue();
        }

        using var secondContext = _db.CreateContext();
        var second = await BuildService(secondContext, DateTime.UtcNow).SubmitAsync(fixture.Provider.Id, fixture.BookingId, new SubmitCustomerRatingRequest(1, "Changed my mind"));

        second.IsSuccess.Should().BeFalse();
        second.Error.Code.Should().Be("CustomerRating.NotEligible");
    }

    [Fact]
    public async Task SubmitAsync_rejects_a_provider_who_is_not_the_assigned_provider_on_this_booking()
    {
        Fixture fixture;
        using (var context = _db.CreateContext())
        {
            fixture = SeedBooking(context, BookingStatus.Completed, DateTime.UtcNow.AddDays(-1));
        }

        using var context2 = _db.CreateContext();
        var someoneElsesId = Guid.NewGuid();
        var result = await BuildService(context2, DateTime.UtcNow).SubmitAsync(someoneElsesId, fixture.BookingId, new SubmitCustomerRatingRequest(3, null));

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("CustomerRating.BookingNotFound");
    }

    [Fact]
    public async Task SubmitAsync_rejects_a_rating_submitted_after_the_configured_window()
    {
        Fixture fixture;
        using (var context = _db.CreateContext())
        {
            fixture = SeedBooking(context, BookingStatus.Completed, DateTime.UtcNow.AddDays(-40));
        }

        var policy = new ReviewPolicyOptions { EnforceSubmissionWindow = true, SubmissionWindowDays = 30 };

        using var context2 = _db.CreateContext();
        var result = await BuildService(context2, DateTime.UtcNow, policy).SubmitAsync(fixture.Provider.Id, fixture.BookingId, new SubmitCustomerRatingRequest(3, null));

        result.IsSuccess.Should().BeFalse();
        result.Error.Message.Should().Contain("window");
    }

    [Fact]
    public async Task GetByBookingAsync_returns_null_when_nothing_was_submitted_yet()
    {
        Fixture fixture;
        using (var context = _db.CreateContext())
        {
            fixture = SeedBooking(context, BookingStatus.Completed, DateTime.UtcNow);
        }

        using var readContext = _db.CreateContext();
        var result = await BuildService(readContext, DateTime.UtcNow).GetByBookingAsync(fixture.Provider.Id, fixture.BookingId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }

    [Fact]
    public async Task GetSummaryForCustomerAsync_returns_null_before_any_rating_and_the_aggregate_after()
    {
        Fixture fixture;
        using (var context = _db.CreateContext())
        {
            fixture = SeedBooking(context, BookingStatus.Completed, DateTime.UtcNow.AddDays(-2));
        }

        using (var context = _db.CreateContext())
        {
            var before = await new CustomerRatingRepository(context).GetSummaryForCustomerAsync(fixture.Customer.Id);
            before.Should().BeNull();
        }

        using (var context = _db.CreateContext())
        {
            var submit = await BuildService(context, DateTime.UtcNow).SubmitAsync(fixture.Provider.Id, fixture.BookingId, new SubmitCustomerRatingRequest(5, null));
            submit.IsSuccess.Should().BeTrue();
        }

        using var readContext = _db.CreateContext();
        var summary = await new CustomerRatingRepository(readContext).GetSummaryForCustomerAsync(fixture.Customer.Id);

        summary.Should().NotBeNull();
        summary!.RatingCount.Should().Be(1);
        summary.AverageRating.Should().Be(5);
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;
        public FakeTimeProvider(DateTime now) => _now = new DateTimeOffset(DateTime.SpecifyKind(now, DateTimeKind.Utc));
        public override DateTimeOffset GetUtcNow() => _now;
    }
}

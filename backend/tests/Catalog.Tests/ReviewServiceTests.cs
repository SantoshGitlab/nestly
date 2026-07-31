using FluentAssertions;
using Microsoft.Extensions.Options;
using Nestly.Application;
using Nestly.Application.Reviews;
using Nestly.Domain;
using Nestly.Infrastructure.Options;
using Nestly.Infrastructure.Persistence.Repositories;
using Nestly.Infrastructure.Services;

namespace Nestly.Catalog.Tests;

/// <summary>Covers tasks 85a-c: completed-booking eligibility, one primary review per booking, optional time window.</summary>
public sealed class ReviewServiceTests : IClassFixture<TestDatabase>
{
    private readonly TestDatabase _db;

    public ReviewServiceTests(TestDatabase db) => _db = db;

    private sealed record Fixture(Customer Customer, Guid BookingId, Guid ServiceId, DateTime CompletedAtUtc);

    private Fixture SeedBooking(Nestly.Infrastructure.Persistence.NestlyDbContext context, BookingStatus finalStatus, DateTime completedAt)
    {
        var customer = new Customer(Guid.NewGuid(), "9" + Guid.NewGuid().ToString("N")[..9], "Asha Rao", CustomerStatus.Active);
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

        if (finalStatus == BookingStatus.Completed)
        {
            booking.TransitionTo(BookingStatus.AwaitingFulfilment);
            booking.TransitionTo(BookingStatus.Assigned);
            booking.TransitionTo(BookingStatus.InProgress);
            booking.TransitionTo(BookingStatus.Completed);
        }

        context.Add(customer);
        context.Add(category);
        context.Add(service);
        context.Add(booking);
        context.SaveChanges();

        // Backdate the Completed history row directly so the submission-window test controls "now" relative to it.
        if (finalStatus == BookingStatus.Completed)
        {
            var completedRow = context.Set<BookingStatusHistory>().Single(h => h.BookingId == booking.Id && h.ToStatus == BookingStatus.Completed);
            typeof(BookingStatusHistory).GetProperty(nameof(BookingStatusHistory.ChangedAtUtc))!.SetValue(completedRow, completedAt);
            context.SaveChanges();
        }

        return new Fixture(customer, booking.Id, service.Id, completedAt);
    }

    private static ReviewService BuildService(Nestly.Infrastructure.Persistence.NestlyDbContext context, DateTime now, ReviewPolicyOptions? policy = null) =>
        new(new BookingRepository(context), new ReviewRepository(context), new FakeTimeProvider(now), Options.Create(policy ?? new ReviewPolicyOptions()));

    [Fact]
    public async Task Only_a_completed_booking_is_eligible_for_review()
    {
        Fixture fixture;
        using (var context = _db.CreateContext())
        {
            fixture = SeedBooking(context, BookingStatus.Confirmed, DateTime.UtcNow);
        }

        using var readContext = _db.CreateContext();
        var result = await BuildService(readContext, DateTime.UtcNow).GetEligibilityAsync(fixture.Customer.Id, fixture.BookingId);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsEligible.Should().BeFalse();
        result.Value.IneligibilityReason.Should().Contain("completed");
    }

    [Fact]
    public async Task SubmitAsync_succeeds_for_a_completed_booking_within_the_window()
    {
        Fixture fixture;
        using (var context = _db.CreateContext())
        {
            fixture = SeedBooking(context, BookingStatus.Completed, DateTime.UtcNow.AddDays(-5));
        }

        using var context2 = _db.CreateContext();
        var result = await BuildService(context2, DateTime.UtcNow).SubmitAsync(
            fixture.Customer.Id, fixture.BookingId, new SubmitReviewRequest(5, "Excellent!", "punctual,friendly"));

        result.IsSuccess.Should().BeTrue();
        result.Value.Rating.Should().Be(5);
        result.Value.ServiceId.Should().Be(fixture.ServiceId);
        result.Value.Status.Should().Be(ReviewStatus.Visible);
    }

    [Fact]
    public async Task SubmitAsync_rejects_a_second_review_for_the_same_booking()
    {
        Fixture fixture;
        using (var context = _db.CreateContext())
        {
            fixture = SeedBooking(context, BookingStatus.Completed, DateTime.UtcNow.AddDays(-1));
        }

        using (var firstContext = _db.CreateContext())
        {
            var first = await BuildService(firstContext, DateTime.UtcNow).SubmitAsync(fixture.Customer.Id, fixture.BookingId, new SubmitReviewRequest(4, "Good", null));
            first.IsSuccess.Should().BeTrue();
        }

        using var secondContext = _db.CreateContext();
        var second = await BuildService(secondContext, DateTime.UtcNow).SubmitAsync(fixture.Customer.Id, fixture.BookingId, new SubmitReviewRequest(2, "Changed my mind", null));

        second.IsSuccess.Should().BeFalse();
        second.Error.Code.Should().Be("Review.NotEligible");
    }

    [Fact]
    public async Task SubmitAsync_rejects_a_review_submitted_after_the_configured_window()
    {
        Fixture fixture;
        using (var context = _db.CreateContext())
        {
            fixture = SeedBooking(context, BookingStatus.Completed, DateTime.UtcNow.AddDays(-40));
        }

        var policy = new ReviewPolicyOptions { EnforceSubmissionWindow = true, SubmissionWindowDays = 30 };

        using var context2 = _db.CreateContext();
        var result = await BuildService(context2, DateTime.UtcNow, policy).SubmitAsync(fixture.Customer.Id, fixture.BookingId, new SubmitReviewRequest(3, null, null));

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
        var result = await BuildService(readContext, DateTime.UtcNow).GetByBookingAsync(fixture.Customer.Id, fixture.BookingId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;
        public FakeTimeProvider(DateTime now) => _now = new DateTimeOffset(DateTime.SpecifyKind(now, DateTimeKind.Utc));
        public override DateTimeOffset GetUtcNow() => _now;
    }
}

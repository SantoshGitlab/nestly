using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Nestly.Application;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence.Repositories;

namespace Nestly.Catalog.Tests;

/// <summary>Covers task 84a-d: review, support_ticket, support_ticket_comment, notification_event schema.</summary>
public sealed class SupportNotificationSchemaTests : IClassFixture<TestDatabase>
{
    private readonly TestDatabase _db;

    public SupportNotificationSchemaTests(TestDatabase db) => _db = db;

    private static (Customer Customer, Booking Booking, Service Service) SeedCustomerAndBooking(Nestly.Infrastructure.Persistence.NestlyDbContext context)
    {
        var customer = new Customer(Guid.NewGuid(), "9" + Guid.NewGuid().ToString("N")[..9], "Asha Rao", CustomerStatus.Active);
        var category = new Category(Guid.NewGuid(), "Cleaning", "cleaning-" + Guid.NewGuid(), "desc");
        var service = new Service(Guid.NewGuid(), category.Id, "Deep Clean", "deep-clean-" + Guid.NewGuid(), "desc", 999m);

        var booking = new Booking(
            Guid.NewGuid(), customer.Id,
            new CustomerSnapshot(customer.Name, customer.Mobile),
            null,
            new AddressSnapshot("Home", "221B Baker Street", null, null, "560001", "Bengaluru", "Karnataka", 12.9716m, 77.5946m, "Asha Rao", "9876543210"),
            new SlotSnapshot(Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)), "Morning", TimeSpan.FromHours(9), TimeSpan.FromHours(13)),
            new PriceSnapshot(999m, 1, 999m, 0m, 0m, 999m, 0m, 0m, 0m, 999m));
        booking.AddItem(Guid.NewGuid(), service.Id, service.Name, service.Slug, 999m, 1);
        booking.TransitionTo(BookingStatus.PaymentPending);
        booking.TransitionTo(BookingStatus.Confirmed);
        booking.TransitionTo(BookingStatus.AwaitingFulfilment);
        booking.TransitionTo(BookingStatus.Assigned);
        booking.TransitionTo(BookingStatus.InProgress);
        booking.TransitionTo(BookingStatus.Completed);

        context.Add(customer);
        context.Add(category);
        context.Add(service);
        context.Add(booking);
        context.SaveChanges();

        return (customer, booking, service);
    }

    [Fact]
    public async Task Review_round_trips_and_enforces_one_primary_review_per_booking()
    {
        (Customer customer, Booking booking, Service service) seed;
        using (var context = _db.CreateContext())
        {
            seed = SeedCustomerAndBooking(context);
        }

        var review = new Review(Guid.NewGuid(), seed.booking.Id, seed.customer.Id, seed.service.Id, providerId: null, 5, "Great service!");
        using (var context = _db.CreateContext())
        {
            await new ReviewRepository(context).AddAsync(review);
        }

        using (var readContext = _db.CreateContext())
        {
            var loaded = await new ReviewRepository(readContext).GetByBookingIdAsync(seed.booking.Id);
            loaded.Should().NotBeNull();
            loaded!.Rating.Should().Be(5);
            loaded.Status.Should().Be(ReviewStatus.Visible);
        }

        var duplicate = new Review(Guid.NewGuid(), seed.booking.Id, seed.customer.Id, seed.service.Id, providerId: null, 1, "Second attempt");
        using var duplicateContext = _db.CreateContext();
        var act = async () => await new ReviewRepository(duplicateContext).AddAsync(duplicate);

        await act.Should().ThrowAsync<DbUpdateException>("a booking may have at most one primary review");
    }

    [Fact]
    public void Review_rejects_a_rating_outside_1_to_5()
    {
        var act = () => new Review(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), providerId: null, 6, null);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task SupportTicket_round_trips_with_its_comment_thread()
    {
        Customer customer;
        Booking booking;
        using (var context = _db.CreateContext())
        {
            (customer, booking, _) = SeedCustomerAndBooking(context);
        }

        var ticket = new SupportTicket(Guid.NewGuid(), customer.Id, booking.Id, SupportTicketCategory.RefundIssue, "Refund not received", "It has been 5 days.");
        ticket.AddComment(Guid.NewGuid(), SupportTicketCommentAuthorType.Customer, "Any update?");
        ticket.AddComment(Guid.NewGuid(), SupportTicketCommentAuthorType.Support, "Looking into it.");

        using (var context = _db.CreateContext())
        {
            await new SupportTicketRepository(context).AddAsync(ticket);
        }

        using var readContext = _db.CreateContext();
        var loaded = await new SupportTicketRepository(readContext).GetByIdAsync(ticket.Id);

        loaded.Should().NotBeNull();
        loaded!.Comments.Should().HaveCount(2);
        loaded.Status.Should().Be(SupportTicketStatus.Open);
        loaded.Category.Should().Be(SupportTicketCategory.RefundIssue);
    }

    [Theory]
    [InlineData(SupportTicketStatus.Open, SupportTicketStatus.InProgress, true)]
    [InlineData(SupportTicketStatus.InProgress, SupportTicketStatus.WaitingForCustomer, true)]
    [InlineData(SupportTicketStatus.InProgress, SupportTicketStatus.Resolved, true)]
    [InlineData(SupportTicketStatus.Resolved, SupportTicketStatus.Closed, true)]
    [InlineData(SupportTicketStatus.Open, SupportTicketStatus.Escalated, true)]
    [InlineData(SupportTicketStatus.Closed, SupportTicketStatus.Open, false)]
    [InlineData(SupportTicketStatus.Open, SupportTicketStatus.Resolved, false)]
    public void SupportTicketLifecycle_matches_SRS_31_2_transition_examples(SupportTicketStatus from, SupportTicketStatus to, bool expected)
    {
        SupportTicketLifecycle.IsValidTransition(from, to).Should().Be(expected);
    }

    [Fact]
    public void MarkDisputed_then_ResolveDispute_resolves_the_ticket_with_the_recorded_outcome()
    {
        var ticket = new SupportTicket(Guid.NewGuid(), Guid.NewGuid(), null, SupportTicketCategory.PricingDispute, "Wrong charge", "I was charged twice.");

        ticket.MarkDisputed();
        ticket.IsDisputed.Should().BeTrue();

        ticket.ResolveDispute(DisputeResolutionOutcome.RefundValid, "Duplicate charge confirmed - refund issued.");

        ticket.Status.Should().Be(SupportTicketStatus.Resolved);
        ticket.DisputeOutcome.Should().Be(DisputeResolutionOutcome.RefundValid);
        ticket.DisputeResolvedAtUtc.Should().NotBeNull();
        ticket.ResolutionSummary.Should().Be("Duplicate charge confirmed - refund issued.");
    }

    [Fact]
    public void ResolveDispute_without_MarkDisputed_first_is_rejected()
    {
        var ticket = new SupportTicket(Guid.NewGuid(), Guid.NewGuid(), null, SupportTicketCategory.PricingDispute, "Wrong charge", "I was charged twice.");

        var act = () => ticket.ResolveDispute(DisputeResolutionOutcome.RefundValid, "n/a");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task NotificationEvent_round_trips_and_tracks_delivery_status()
    {
        Customer customer;
        using (var context = _db.CreateContext())
        {
            (customer, _, _) = SeedCustomerAndBooking(context);
        }

        var notification = new NotificationEvent(
            Guid.NewGuid(), customer.Id, NotificationEventType.Welcome, NotificationChannel.Sms, "******7890", "welcome_sms", "{\"name\":\"Asha\"}");
        notification.MarkSent();

        using (var context = _db.CreateContext())
        {
            await new NotificationEventRepository(context).AddAsync(notification);
        }

        using var readContext = _db.CreateContext();
        var list = await new NotificationEventRepository(readContext).ListByCustomerAsync(customer.Id);

        list.Should().ContainSingle();
        list[0].Status.Should().Be(NotificationDeliveryStatus.Sent);
        list[0].SentAtUtc.Should().NotBeNull();
    }
}

using FluentAssertions;
using Nestly.Application;
using Nestly.Application.Support;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence.Repositories;
using Nestly.Infrastructure.Services;

namespace Nestly.Catalog.Tests;

/// <summary>Covers tasks 86a-d: create ticket (categories), list, detail, booking-linked vs. generic tickets.</summary>
public sealed class SupportTicketServiceTests : IClassFixture<TestDatabase>
{
    private readonly TestDatabase _db;

    public SupportTicketServiceTests(TestDatabase db) => _db = db;

    private static SupportTicketService BuildService(Nestly.Infrastructure.Persistence.NestlyDbContext context) =>
        new(new SupportTicketRepository(context), new BookingRepository(context));

    private Guid SeedCustomer(Nestly.Infrastructure.Persistence.NestlyDbContext context)
    {
        var customer = new Customer(Guid.NewGuid(), "9" + Guid.NewGuid().ToString("N")[..9], "Asha Rao", CustomerStatus.Active);
        context.Add(customer);
        context.SaveChanges();
        return customer.Id;
    }

    private Guid SeedBooking(Nestly.Infrastructure.Persistence.NestlyDbContext context, Guid customerId)
    {
        var category = new Category(Guid.NewGuid(), "Cleaning", "cleaning-" + Guid.NewGuid(), "desc");
        var service = new Service(Guid.NewGuid(), category.Id, "Deep Clean", "deep-clean-" + Guid.NewGuid(), "desc", 999m);
        var customer = context.Set<Customer>().Single(c => c.Id == customerId);

        var booking = new Booking(
            Guid.NewGuid(), customerId,
            new CustomerSnapshot(customer.Name, customer.Mobile),
            null,
            new AddressSnapshot("Home", "221B Baker Street", null, null, "560001", "Bengaluru", "Karnataka", 12.9716m, 77.5946m, "Asha Rao", "9876543210"),
            new SlotSnapshot(Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)), "Morning", TimeSpan.FromHours(9), TimeSpan.FromHours(13)),
            new PriceSnapshot(999m, 1, 999m, 0m, 0m, 999m, 0m, 0m, 0m, 999m));
        booking.AddItem(Guid.NewGuid(), service.Id, service.Name, service.Slug, 999m, 1);

        context.Add(category);
        context.Add(service);
        context.Add(booking);
        context.SaveChanges();

        return booking.Id;
    }

    [Fact]
    public async Task CreateAsync_rejects_a_booking_linked_category_with_no_booking_reference()
    {
        Guid customerId;
        using (var context = _db.CreateContext())
        {
            customerId = SeedCustomer(context);
        }

        using var readContext = _db.CreateContext();
        var result = await BuildService(readContext).CreateAsync(
            customerId, new CreateSupportTicketRequest(SupportTicketCategory.RefundIssue, null, "Refund missing", "Not received yet.", null));

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("SupportTicket.BookingRequired");
    }

    [Fact]
    public async Task CreateAsync_allows_a_generic_category_with_no_booking_reference()
    {
        Guid customerId;
        using (var context = _db.CreateContext())
        {
            customerId = SeedCustomer(context);
        }

        using var readContext = _db.CreateContext();
        var result = await BuildService(readContext).CreateAsync(
            customerId, new CreateSupportTicketRequest(SupportTicketCategory.GeneralInquiry, null, "How does pricing work?", "Just curious.", null));

        result.IsSuccess.Should().BeTrue();
        result.Value.BookingId.Should().BeNull();
        result.Value.Status.Should().Be(SupportTicketStatus.Open);
        result.Value.Priority.Should().Be(SupportTicketPriority.Normal);
    }

    [Fact]
    public async Task CreateAsync_links_a_booking_issue_ticket_to_its_booking()
    {
        Guid customerId, bookingId;
        using (var context = _db.CreateContext())
        {
            customerId = SeedCustomer(context);
            bookingId = SeedBooking(context, customerId);
        }

        using var readContext = _db.CreateContext();
        var result = await BuildService(readContext).CreateAsync(
            customerId, new CreateSupportTicketRequest(SupportTicketCategory.BookingIssue, bookingId, "Wrong address used", "Provider went to the wrong flat.", SupportTicketPriority.High));

        result.IsSuccess.Should().BeTrue();
        result.Value.BookingId.Should().Be(bookingId);
        result.Value.Priority.Should().Be(SupportTicketPriority.High);
    }

    [Fact]
    public async Task ListAsync_returns_only_the_callers_own_tickets()
    {
        Guid customerAId, customerBId;
        using (var context = _db.CreateContext())
        {
            customerAId = SeedCustomer(context);
            customerBId = SeedCustomer(context);
        }

        using (var context = _db.CreateContext())
        {
            await BuildService(context).CreateAsync(customerAId, new CreateSupportTicketRequest(SupportTicketCategory.GeneralInquiry, null, "A's ticket", "desc", null));
            await BuildService(context).CreateAsync(customerBId, new CreateSupportTicketRequest(SupportTicketCategory.GeneralInquiry, null, "B's ticket", "desc", null));
        }

        using var readContext = _db.CreateContext();
        var result = await BuildService(readContext).ListAsync(customerAId);

        result.Value.Should().ContainSingle(t => t.Subject == "A's ticket");
    }

    [Fact]
    public async Task ListByBookingAsync_scopes_to_the_booking_and_rejects_another_customers_booking()
    {
        Guid customerId, bookingId;
        using (var context = _db.CreateContext())
        {
            customerId = SeedCustomer(context);
            bookingId = SeedBooking(context, customerId);
        }

        using (var context = _db.CreateContext())
        {
            await BuildService(context).CreateAsync(customerId, new CreateSupportTicketRequest(SupportTicketCategory.PaymentIssue, bookingId, "Double charged", "desc", null));
        }

        using (var context = _db.CreateContext())
        {
            var result = await BuildService(context).ListByBookingAsync(customerId, bookingId);
            result.Value.Should().ContainSingle();
        }

        using var otherContext = _db.CreateContext();
        var forbidden = await BuildService(otherContext).ListByBookingAsync(Guid.NewGuid(), bookingId);
        forbidden.IsSuccess.Should().BeFalse();
        forbidden.Error.Code.Should().Be("SupportTicket.BookingNotFound");
    }

    [Fact]
    public async Task AddCommentAsync_appends_to_the_thread_across_a_fresh_context_without_corrupting_the_ticket_row()
    {
        // Regression test for the EF Core change-tracking gap
        // NewOwnedChildEntityInterceptor exists to fix: a comment appended
        // to a ticket that was loaded (tracked, not Added) by a *different*
        // DbContext instance than the one that originally created it - the
        // realistic shape of two separate HTTP requests.
        Guid customerId;
        Guid ticketId;
        using (var createContext = _db.CreateContext())
        {
            customerId = SeedCustomer(createContext);
            var created = await BuildService(createContext).CreateAsync(
                customerId, new CreateSupportTicketRequest(SupportTicketCategory.GeneralInquiry, null, "Question", "desc", null));
            ticketId = created.Value.Id;
        }

        using (var commentContext = _db.CreateContext())
        {
            var result = await BuildService(commentContext).AddCommentAsync(customerId, ticketId, new AddSupportTicketCommentRequest("Any update on this?"));
            result.IsSuccess.Should().BeTrue();
            result.Value.Comments.Should().ContainSingle(c => c.Comment == "Any update on this?" && c.AuthorType == SupportTicketCommentAuthorType.Customer);
        }

        using var readContext = _db.CreateContext();
        var detail = await BuildService(readContext).GetDetailAsync(customerId, ticketId);

        detail.IsSuccess.Should().BeTrue();
        detail.Value.Subject.Should().Be("Question", "the parent ticket row must not have been corrupted by the comment insert");
        detail.Value.Comments.Should().ContainSingle();
    }

    [Fact]
    public async Task GetDetailAsync_rejects_another_customers_ticket()
    {
        Guid customerId, ticketId;
        using (var context = _db.CreateContext())
        {
            customerId = SeedCustomer(context);
            var created = await BuildService(context).CreateAsync(customerId, new CreateSupportTicketRequest(SupportTicketCategory.GeneralInquiry, null, "Q", "desc", null));
            ticketId = created.Value.Id;
        }

        using var readContext = _db.CreateContext();
        var result = await BuildService(readContext).GetDetailAsync(Guid.NewGuid(), ticketId);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("SupportTicket.NotFound");
    }
}

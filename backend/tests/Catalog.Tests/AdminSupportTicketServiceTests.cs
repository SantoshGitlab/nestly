using FluentAssertions;
using Nestly.Application;
using Nestly.Application.Abstractions.Auditing;
using Nestly.Application.Support;
using Nestly.Domain;
using Nestly.Infrastructure.Auditing;
using Nestly.Infrastructure.Persistence.Repositories;
using Nestly.Infrastructure.Services;

namespace Nestly.Catalog.Tests;

/// <summary>Covers tasks 120a-e: the general admin ticket workflow - assign/unassign, respond, escalate, resolve/close, and link a booking - kept separate from <see cref="DisputeResolutionServiceTests"/> (task 155's formal dispute sub-flow).</summary>
public sealed class AdminSupportTicketServiceTests : IClassFixture<TestDatabase>
{
    private readonly TestDatabase _db;

    public AdminSupportTicketServiceTests(TestDatabase db) => _db = db;

    private static AdminSupportTicketService BuildService(Nestly.Infrastructure.Persistence.NestlyDbContext context) =>
        new(
            new SupportTicketRepository(context),
            new AdminUserRepository(context),
            new BookingRepository(context),
            new AuditLogWriter(context, new StubAuditContextProvider()));

    private sealed class StubAuditContextProvider : IAuditContextProvider
    {
        public AuditContext GetCurrent() =>
            new(AuditActorType.AdminUser, Guid.NewGuid(), IpAddress: "127.0.0.1", CorrelationId: "test-correlation-id");
    }

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

    private Guid SeedAdmin(Nestly.Infrastructure.Persistence.NestlyDbContext context, AdminUserStatus status = AdminUserStatus.Active, string fullName = "Priya Nair")
    {
        var admin = new AdminUser(Guid.NewGuid(), $"agent-{Guid.NewGuid():N}@nestly.test", "hashed-password", fullName);
        if (status == AdminUserStatus.Inactive)
        {
            admin.Deactivate();
        }

        context.Add(admin);
        context.SaveChanges();
        return admin.Id;
    }

    private Guid SeedTicket(Nestly.Infrastructure.Persistence.NestlyDbContext context, Guid customerId, Guid? bookingId = null)
    {
        var ticket = new SupportTicket(Guid.NewGuid(), customerId, bookingId, SupportTicketCategory.GeneralInquiry, "Question", "Just curious about pricing.");
        new SupportTicketRepository(context).AddAsync(ticket).GetAwaiter().GetResult();
        return ticket.Id;
    }

    [Fact]
    public async Task AssignAsync_assigns_a_ticket_to_an_active_admin()
    {
        Guid customerId, ticketId, adminId;
        using (var context = _db.CreateContext())
        {
            customerId = SeedCustomer(context);
            ticketId = SeedTicket(context, customerId);
            adminId = SeedAdmin(context);
        }

        using var context2 = _db.CreateContext();
        var result = await BuildService(context2).AssignAsync(ticketId, new AssignSupportTicketRequest(adminId));

        result.IsSuccess.Should().BeTrue();
        result.Value.AssignedAdminUserId.Should().Be(adminId);
        result.Value.AssignedAdminName.Should().Be("Priya Nair");
        result.Value.AssignedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task AssignAsync_rejects_an_inactive_admin()
    {
        Guid customerId, ticketId, adminId;
        using (var context = _db.CreateContext())
        {
            customerId = SeedCustomer(context);
            ticketId = SeedTicket(context, customerId);
            adminId = SeedAdmin(context, AdminUserStatus.Inactive);
        }

        using var context2 = _db.CreateContext();
        var result = await BuildService(context2).AssignAsync(ticketId, new AssignSupportTicketRequest(adminId));

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("SupportTicket.AssigneeNotFound");
    }

    [Fact]
    public async Task AssignAsync_rejects_an_unknown_admin()
    {
        Guid customerId, ticketId;
        using (var context = _db.CreateContext())
        {
            customerId = SeedCustomer(context);
            ticketId = SeedTicket(context, customerId);
        }

        using var context2 = _db.CreateContext();
        var result = await BuildService(context2).AssignAsync(ticketId, new AssignSupportTicketRequest(Guid.NewGuid()));

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("SupportTicket.AssigneeNotFound");
    }

    [Fact]
    public async Task UnassignAsync_clears_a_ticket_assignment()
    {
        Guid customerId, ticketId, adminId;
        using (var context = _db.CreateContext())
        {
            customerId = SeedCustomer(context);
            ticketId = SeedTicket(context, customerId);
            adminId = SeedAdmin(context);
        }

        using (var assignContext = _db.CreateContext())
        {
            var assign = await BuildService(assignContext).AssignAsync(ticketId, new AssignSupportTicketRequest(adminId));
            assign.IsSuccess.Should().BeTrue();
        }

        using var context2 = _db.CreateContext();
        var result = await BuildService(context2).UnassignAsync(ticketId);

        result.IsSuccess.Should().BeTrue();
        result.Value.AssignedAdminUserId.Should().BeNull();
        result.Value.AssignedAdminName.Should().BeNull();
    }

    [Fact]
    public async Task RespondAsync_appends_a_support_authored_comment()
    {
        Guid customerId, ticketId;
        using (var context = _db.CreateContext())
        {
            customerId = SeedCustomer(context);
            ticketId = SeedTicket(context, customerId);
        }

        using var context2 = _db.CreateContext();
        var result = await BuildService(context2).RespondAsync(ticketId, new AddSupportTicketCommentRequest("We're looking into this."));

        result.IsSuccess.Should().BeTrue();
        result.Value.Comments.Should().ContainSingle(c => c.Comment == "We're looking into this." && c.AuthorType == SupportTicketCommentAuthorType.Support);
    }

    [Fact]
    public async Task EscalateAsync_moves_an_open_ticket_to_escalated_and_stamps_the_time()
    {
        Guid customerId, ticketId;
        using (var context = _db.CreateContext())
        {
            customerId = SeedCustomer(context);
            ticketId = SeedTicket(context, customerId);
        }

        using var context2 = _db.CreateContext();
        var result = await BuildService(context2).EscalateAsync(ticketId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(SupportTicketStatus.Escalated);
        result.Value.EscalatedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task EscalateAsync_rejects_a_ticket_that_is_already_closed()
    {
        Guid customerId, ticketId;
        using (var context = _db.CreateContext())
        {
            customerId = SeedCustomer(context);
            ticketId = SeedTicket(context, customerId);
        }

        using (var closeContext = _db.CreateContext())
        {
            var close = await BuildService(closeContext).CloseAsync(ticketId);
            close.IsSuccess.Should().BeTrue("Open -> Closed is a valid direct transition (e.g. duplicate/spam)");
        }

        using var context2 = _db.CreateContext();
        var result = await BuildService(context2).EscalateAsync(ticketId);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("SupportTicket.CannotEscalate");
    }

    [Fact]
    public async Task ResolveAsync_moves_a_ticket_already_in_progress_to_resolved_with_a_summary()
    {
        // SupportTicketLifecycle has no direct Open -> Resolved edge - an
        // admin response is what moves a fresh ticket into InProgress first
        // (RespondAsync's documented auto-transition), the same as a real
        // admin would do before resolving.
        Guid customerId, ticketId;
        using (var context = _db.CreateContext())
        {
            customerId = SeedCustomer(context);
            ticketId = SeedTicket(context, customerId);
        }

        using (var respondContext = _db.CreateContext())
        {
            var respond = await BuildService(respondContext).RespondAsync(ticketId, new AddSupportTicketCommentRequest("Looking into this."));
            respond.IsSuccess.Should().BeTrue();
            respond.Value.Status.Should().Be(SupportTicketStatus.InProgress);
        }

        using var context2 = _db.CreateContext();
        var result = await BuildService(context2).ResolveAsync(ticketId, new ResolveSupportTicketRequest("Explained the pricing model to the customer."));

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(SupportTicketStatus.Resolved);
        result.Value.ResolutionSummary.Should().Be("Explained the pricing model to the customer.");
    }

    [Fact]
    public async Task ResolveAsync_rejects_a_still_open_ticket_with_no_prior_activity()
    {
        Guid customerId, ticketId;
        using (var context = _db.CreateContext())
        {
            customerId = SeedCustomer(context);
            ticketId = SeedTicket(context, customerId);
        }

        using var context2 = _db.CreateContext();
        var result = await BuildService(context2).ResolveAsync(ticketId, new ResolveSupportTicketRequest("n/a"));

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("SupportTicket.CannotResolve");
    }

    [Fact]
    public async Task CloseAsync_moves_a_resolved_ticket_to_closed()
    {
        Guid customerId, ticketId;
        using (var context = _db.CreateContext())
        {
            customerId = SeedCustomer(context);
            ticketId = SeedTicket(context, customerId);
        }

        using (var respondContext = _db.CreateContext())
        {
            var respond = await BuildService(respondContext).RespondAsync(ticketId, new AddSupportTicketCommentRequest("Looking into this."));
            respond.IsSuccess.Should().BeTrue();
        }

        using (var resolveContext = _db.CreateContext())
        {
            var resolve = await BuildService(resolveContext).ResolveAsync(ticketId, new ResolveSupportTicketRequest("Done."));
            resolve.IsSuccess.Should().BeTrue();
        }

        using var context2 = _db.CreateContext();
        var result = await BuildService(context2).CloseAsync(ticketId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(SupportTicketStatus.Closed);
    }

    [Fact]
    public async Task CloseAsync_allows_closing_a_still_open_ticket_directly_as_duplicate_or_spam()
    {
        Guid customerId, ticketId;
        using (var context = _db.CreateContext())
        {
            customerId = SeedCustomer(context);
            ticketId = SeedTicket(context, customerId);
        }

        using var context2 = _db.CreateContext();
        var result = await BuildService(context2).CloseAsync(ticketId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(SupportTicketStatus.Closed);
    }

    [Fact]
    public async Task LinkBookingAsync_attaches_a_booking_belonging_to_the_tickets_customer()
    {
        Guid customerId, ticketId, bookingId;
        using (var context = _db.CreateContext())
        {
            customerId = SeedCustomer(context);
            bookingId = SeedBooking(context, customerId);
            ticketId = SeedTicket(context, customerId);
        }

        using var context2 = _db.CreateContext();
        var result = await BuildService(context2).LinkBookingAsync(ticketId, new LinkSupportTicketBookingRequest(bookingId));

        result.IsSuccess.Should().BeTrue();
        result.Value.BookingId.Should().Be(bookingId);
        result.Value.Booking.Should().NotBeNull();
        result.Value.Booking!.Id.Should().Be(bookingId);
    }

    [Fact]
    public async Task LinkBookingAsync_rejects_a_booking_belonging_to_a_different_customer()
    {
        Guid customerAId, customerBId, ticketId, bookingId;
        using (var context = _db.CreateContext())
        {
            customerAId = SeedCustomer(context);
            customerBId = SeedCustomer(context);
            bookingId = SeedBooking(context, customerBId);
            ticketId = SeedTicket(context, customerAId);
        }

        using var context2 = _db.CreateContext();
        var result = await BuildService(context2).LinkBookingAsync(ticketId, new LinkSupportTicketBookingRequest(bookingId));

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("SupportTicket.BookingNotFound");
    }

    [Fact]
    public async Task GetDetailAsync_returns_not_found_for_an_unknown_ticket()
    {
        using var context = _db.CreateContext();
        var result = await BuildService(context).GetDetailAsync(Guid.NewGuid());

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("SupportTicket.NotFound");
    }

    [Fact]
    public async Task SearchAsync_filters_by_status()
    {
        Guid customerId, openTicketId, resolvedTicketId;
        using (var context = _db.CreateContext())
        {
            customerId = SeedCustomer(context);
            openTicketId = SeedTicket(context, customerId);
            resolvedTicketId = SeedTicket(context, customerId);
        }

        using (var respondContext = _db.CreateContext())
        {
            var respond = await BuildService(respondContext).RespondAsync(resolvedTicketId, new AddSupportTicketCommentRequest("Looking into this."));
            respond.IsSuccess.Should().BeTrue();
        }

        using (var resolveContext = _db.CreateContext())
        {
            var resolve = await BuildService(resolveContext).ResolveAsync(resolvedTicketId, new ResolveSupportTicketRequest("Done."));
            resolve.IsSuccess.Should().BeTrue();
        }

        using var context2 = _db.CreateContext();
        var result = await BuildService(context2).SearchAsync(
            new AdminSupportTicketSearchRequest(customerId, null, null, null, SupportTicketStatus.Resolved, null, null, null, null));

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle(t => t.Id == resolvedTicketId);
        result.Value.Items.Should().NotContain(t => t.Id == openTicketId);
    }

    [Fact]
    public async Task ListAssignableAdminsAsync_returns_only_active_admins()
    {
        Guid activeAdminId, inactiveAdminId;
        using (var context = _db.CreateContext())
        {
            activeAdminId = SeedAdmin(context, fullName: "Active Agent " + Guid.NewGuid());
            inactiveAdminId = SeedAdmin(context, AdminUserStatus.Inactive, "Inactive Agent " + Guid.NewGuid());
        }

        using var context2 = _db.CreateContext();
        var result = await BuildService(context2).ListAssignableAdminsAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain(a => a.Id == activeAdminId);
        result.Value.Should().NotContain(a => a.Id == inactiveAdminId);
    }
}

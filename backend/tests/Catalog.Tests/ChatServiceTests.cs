using FluentAssertions;
using Nestly.Application;
using Nestly.Application.Chat;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence;
using Nestly.Infrastructure.Persistence.Repositories;
using Nestly.Infrastructure.Services;

namespace Nestly.Catalog.Tests;

/// <summary>Covers task 191: get-or-create thread, send, paginated history, mark-read, and ownership enforcement (SupportTicketsController/RefundsController's pattern).</summary>
public sealed class ChatServiceTests : IClassFixture<TestDatabase>
{
    private readonly TestDatabase _db;

    public ChatServiceTests(TestDatabase db) => _db = db;

    private static ChatService BuildService(NestlyDbContext context) => new(
        new ChatThreadRepository(context), new ChatMessageRepository(context),
        new BookingRepository(context), new SupportTicketRepository(context));

    private static AdminChatService BuildAdminService(NestlyDbContext context) => new(
        new ChatThreadRepository(context), new ChatMessageRepository(context),
        new BookingRepository(context), new SupportTicketRepository(context), new CustomerRepository(context));

    private Guid SeedCustomer(NestlyDbContext context)
    {
        var customer = new Customer(Guid.NewGuid(), "9" + Guid.NewGuid().ToString("N")[..9], "Asha Rao", CustomerStatus.Active);
        context.Add(customer);
        context.SaveChanges();
        return customer.Id;
    }

    private Guid SeedBooking(NestlyDbContext context, Guid customerId)
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
    public async Task GetOrCreateThreadAsync_creates_a_thread_for_the_callers_own_booking()
    {
        Guid customerId, bookingId;
        using (var context = _db.CreateContext())
        {
            customerId = SeedCustomer(context);
            bookingId = SeedBooking(context, customerId);
        }

        using var readContext = _db.CreateContext();
        var result = await BuildService(readContext).GetOrCreateThreadAsync(customerId, ChatContextType.Booking, bookingId);

        result.IsSuccess.Should().BeTrue();
        result.Value.ContextType.Should().Be(ChatContextType.Booking);
        result.Value.ContextId.Should().Be(bookingId);
    }

    [Fact]
    public async Task GetOrCreateThreadAsync_is_idempotent_for_the_same_context()
    {
        Guid customerId, bookingId;
        using (var context = _db.CreateContext())
        {
            customerId = SeedCustomer(context);
            bookingId = SeedBooking(context, customerId);
        }

        Guid firstThreadId;
        using (var context = _db.CreateContext())
        {
            var first = await BuildService(context).GetOrCreateThreadAsync(customerId, ChatContextType.Booking, bookingId);
            firstThreadId = first.Value.Id;
        }

        using var secondContext = _db.CreateContext();
        var second = await BuildService(secondContext).GetOrCreateThreadAsync(customerId, ChatContextType.Booking, bookingId);

        second.Value.Id.Should().Be(firstThreadId, "a second request for the same context must return the existing thread, not create a duplicate");
    }

    [Fact]
    public async Task GetOrCreateThreadAsync_rejects_a_booking_that_does_not_belong_to_the_caller()
    {
        Guid ownerId, otherId, bookingId;
        using (var context = _db.CreateContext())
        {
            ownerId = SeedCustomer(context);
            otherId = SeedCustomer(context);
            bookingId = SeedBooking(context, ownerId);
        }

        using var readContext = _db.CreateContext();
        var result = await BuildService(readContext).GetOrCreateThreadAsync(otherId, ChatContextType.Booking, bookingId);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Chat.BookingNotFound");
    }

    [Fact]
    public async Task SendMessageAsync_persists_a_customer_message_and_bumps_thread_recency()
    {
        Guid customerId, bookingId, threadId;
        using (var context = _db.CreateContext())
        {
            customerId = SeedCustomer(context);
            bookingId = SeedBooking(context, customerId);
            var created = await BuildService(context).GetOrCreateThreadAsync(customerId, ChatContextType.Booking, bookingId);
            threadId = created.Value.Id;
        }

        using (var context = _db.CreateContext())
        {
            var sendResult = await BuildService(context).SendMessageAsync(customerId, threadId, new SendChatMessageRequest("Are you on your way?"));
            sendResult.IsSuccess.Should().BeTrue();
            sendResult.Value.SenderType.Should().Be(ChatSenderType.Customer);
            sendResult.Value.SenderId.Should().Be(customerId);
            sendResult.Value.ReadAtUtc.Should().BeNull();
        }

        using var readContext = _db.CreateContext();
        var thread = await new ChatThreadRepository(readContext).GetByIdAsync(threadId);
        thread!.LastMessageAtUtc.Should().BeAfter(thread.CreatedAtUtc.AddSeconds(-1));
    }

    [Fact]
    public async Task SendMessageAsync_rejects_a_thread_the_caller_does_not_own()
    {
        Guid ownerId, otherId, bookingId, threadId;
        using (var context = _db.CreateContext())
        {
            ownerId = SeedCustomer(context);
            otherId = SeedCustomer(context);
            bookingId = SeedBooking(context, ownerId);
            var created = await BuildService(context).GetOrCreateThreadAsync(ownerId, ChatContextType.Booking, bookingId);
            threadId = created.Value.Id;
        }

        using var readContext = _db.CreateContext();
        var result = await BuildService(readContext).SendMessageAsync(otherId, threadId, new SendChatMessageRequest("Not yours."));

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Chat.BookingNotFound");
    }

    [Fact]
    public async Task GetHistoryAsync_returns_messages_oldest_first_with_paging_metadata()
    {
        Guid customerId, bookingId, threadId;
        using (var context = _db.CreateContext())
        {
            customerId = SeedCustomer(context);
            bookingId = SeedBooking(context, customerId);
            var created = await BuildService(context).GetOrCreateThreadAsync(customerId, ChatContextType.Booking, bookingId);
            threadId = created.Value.Id;
        }

        using (var context = _db.CreateContext())
        {
            var service = BuildService(context);
            await service.SendMessageAsync(customerId, threadId, new SendChatMessageRequest("First"));
            await service.SendMessageAsync(customerId, threadId, new SendChatMessageRequest("Second"));
        }

        using var readContext = _db.CreateContext();
        var result = await BuildService(readContext).GetHistoryAsync(customerId, threadId, page: 1, pageSize: 50);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(2);
        result.Value.Messages.Should().HaveCount(2);
        result.Value.Messages[0].Body.Should().Be("First");
        result.Value.Messages[1].Body.Should().Be("Second");
    }

    [Fact]
    public async Task MarkReadAsync_sets_read_at_only_on_messages_not_sent_by_the_reader()
    {
        Guid customerId, bookingId, threadId;
        using (var context = _db.CreateContext())
        {
            customerId = SeedCustomer(context);
            bookingId = SeedBooking(context, customerId);
            var created = await BuildService(context).GetOrCreateThreadAsync(customerId, ChatContextType.Booking, bookingId);
            threadId = created.Value.Id;
        }

        using (var context = _db.CreateContext())
        {
            await BuildService(context).SendMessageAsync(customerId, threadId, new SendChatMessageRequest("Customer says hi"));
            await BuildAdminService(context).ReplyAsync(Guid.NewGuid(), threadId, new SendChatMessageRequest("Admin reply"));
        }

        using (var context = _db.CreateContext())
        {
            var markResult = await BuildService(context).MarkReadAsync(customerId, threadId);
            markResult.IsSuccess.Should().BeTrue();
        }

        using var readContext = _db.CreateContext();
        var history = await BuildService(readContext).GetHistoryAsync(customerId, threadId, 1, 50);

        history.Value.Messages.Single(m => m.SenderType == ChatSenderType.Customer).ReadAtUtc.Should().BeNull(
            "the customer's own message must not be marked read by their own mark-read call");
        history.Value.Messages.Single(m => m.SenderType == ChatSenderType.Admin).ReadAtUtc.Should().NotBeNull(
            "the admin's message, addressed to the customer, must be marked read");
    }

    [Fact]
    public async Task AdminChatService_ReplyAsync_can_message_any_customers_thread_without_ownership_restriction()
    {
        Guid customerId, ticketId, threadId;
        using (var context = _db.CreateContext())
        {
            customerId = SeedCustomer(context);
            var ticket = new SupportTicket(Guid.NewGuid(), customerId, null, SupportTicketCategory.GeneralInquiry, "Help", "desc");
            context.Add(ticket);
            context.SaveChanges();
            ticketId = ticket.Id;

            var created = await BuildAdminService(context).GetOrCreateThreadAsync(ChatContextType.SupportTicket, ticketId);
            threadId = created.Value.Id;
        }

        using var replyContext = _db.CreateContext();
        var reply = await BuildAdminService(replyContext).ReplyAsync(Guid.NewGuid(), threadId, new SendChatMessageRequest("We're looking into it."));

        reply.IsSuccess.Should().BeTrue();
        reply.Value.SenderType.Should().Be(ChatSenderType.Admin);
    }

    [Fact]
    public async Task AdminChatService_GetOrCreateThreadAsync_rejects_a_context_that_does_not_exist()
    {
        using var context = _db.CreateContext();
        var result = await BuildAdminService(context).GetOrCreateThreadAsync(ChatContextType.Booking, Guid.NewGuid());

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Chat.BookingNotFound");
    }

    /// <summary>
    /// The inbox's actual job: two different customers each messaging admin
    /// independently must both show up, each correctly attributed - not
    /// merged, not dropped, not cross-attributed to the other's name.
    /// </summary>
    [Fact]
    public async Task ListThreadsAsync_surfaces_threads_from_multiple_customers_most_recent_first()
    {
        Guid firstCustomerId, firstBookingId, secondCustomerId, secondBookingId;
        using (var context = _db.CreateContext())
        {
            firstCustomerId = SeedCustomer(context);
            firstBookingId = SeedBooking(context, firstCustomerId);
            secondCustomerId = SeedCustomer(context);
            secondBookingId = SeedBooking(context, secondCustomerId);
        }

        Guid firstThreadId, secondThreadId;
        using (var context = _db.CreateContext())
        {
            var service = BuildService(context);
            var firstThread = await service.GetOrCreateThreadAsync(firstCustomerId, ChatContextType.Booking, firstBookingId);
            firstThreadId = firstThread.Value.Id;
            await service.SendMessageAsync(firstCustomerId, firstThreadId, new SendChatMessageRequest("From customer one"));
        }

        using (var context = _db.CreateContext())
        {
            var service = BuildService(context);
            var secondThread = await service.GetOrCreateThreadAsync(secondCustomerId, ChatContextType.Booking, secondBookingId);
            secondThreadId = secondThread.Value.Id;
            // Sent after the first customer's message, so recency ordering
            // has something to actually distinguish.
            await service.SendMessageAsync(secondCustomerId, secondThreadId, new SendChatMessageRequest("From customer two"));
        }

        // The class fixture's database is shared across every test in this
        // file (IClassFixture, not reset per test), so other tests' threads
        // are also present here - fetch a page wide enough to contain them
        // all and assert on our own two threads specifically, not on
        // absolute count or position.
        using var readContext = _db.CreateContext();
        var result = await BuildAdminService(readContext).ListThreadsAsync(page: 1, pageSize: 100);

        result.IsSuccess.Should().BeTrue();
        var firstRow = result.Value.Items.Should().ContainSingle(i => i.ThreadId == firstThreadId).Subject;
        var secondRow = result.Value.Items.Should().ContainSingle(i => i.ThreadId == secondThreadId).Subject;

        firstRow.CustomerId.Should().Be(firstCustomerId);
        firstRow.UnreadCount.Should().Be(1, "the admin has not read customer one's message yet");

        secondRow.CustomerId.Should().Be(secondCustomerId);
        secondRow.UnreadCount.Should().Be(1, "the admin has not read customer two's message yet");

        // Each row is attributed to its own customer id (SeedCustomer gives
        // every customer the same display name, so CustomerId - already
        // asserted above - is what actually proves they were not swapped or
        // merged), and recency ordering reflects who messaged more recently.
        secondRow.LastMessageAtUtc.Should().BeAfter(firstRow.LastMessageAtUtc);
        var secondIndex = result.Value.Items.ToList().FindIndex(i => i.ThreadId == secondThreadId);
        var firstIndex = result.Value.Items.ToList().FindIndex(i => i.ThreadId == firstThreadId);
        secondIndex.Should().BeLessThan(firstIndex, "most-recent-first ordering");
    }

    /// <summary>A support-ticket-context thread resolves its customer via SupportTicket.CustomerId, not Booking's snapshot columns - the other branch of the inbox's context-type switch.</summary>
    [Fact]
    public async Task ListThreadsAsync_resolves_customer_name_for_a_support_ticket_context()
    {
        Guid customerId, ticketId;
        using (var context = _db.CreateContext())
        {
            customerId = SeedCustomer(context);
            var ticket = new SupportTicket(Guid.NewGuid(), customerId, null, SupportTicketCategory.GeneralInquiry, "Help", "desc");
            context.Add(ticket);
            context.SaveChanges();
            ticketId = ticket.Id;
        }

        using (var context = _db.CreateContext())
        {
            var adminService = BuildAdminService(context);
            var thread = await adminService.GetOrCreateThreadAsync(ChatContextType.SupportTicket, ticketId);
            await adminService.ReplyAsync(Guid.NewGuid(), thread.Value.Id, new SendChatMessageRequest("How can we help?"));
        }

        using var readContext = _db.CreateContext();
        var result = await BuildAdminService(readContext).ListThreadsAsync(page: 1, pageSize: 100);

        result.IsSuccess.Should().BeTrue();
        var row = result.Value.Items.Should().ContainSingle(i => i.ContextId == ticketId).Subject;
        row.ContextType.Should().Be(ChatContextType.SupportTicket);
        row.CustomerId.Should().Be(customerId);
        row.CustomerName.Should().Be("Asha Rao");
        row.UnreadCount.Should().Be(0, "the only message so far is the admin's own reply, not a customer message awaiting attention");
    }
}

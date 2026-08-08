using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Nestly.Application.Chat;
using Nestly.Domain;
using Nestly.Domain.Events;
using Nestly.Infrastructure.Persistence.Interceptors;
using Nestly.Infrastructure.Realtime;

namespace Nestly.Catalog.Tests;

/// <summary>
/// Task 292: the chat broadcast, mirroring
/// <see cref="BookingTrackingBroadcastHandlerTests"/>. Everything here runs
/// against <see cref="RecordingHubContext"/> - a hand-rolled
/// <see cref="IHubContext{THub}"/> that records frames instead of sending them,
/// so no test opens a socket or reaches Redis. That fake is also deliberately
/// hostile: every <see cref="IHubClients"/> member other than <c>Group</c>
/// throws, so a handler that broadcast to <c>All</c> - putting one thread's
/// private message on every live connection in the process - fails loudly here
/// rather than shipping.
/// </summary>
public sealed class ChatHubBroadcastHandlerTests
{
    private static readonly Guid MessageId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid ThreadId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid ContextId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid SenderId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly DateTime SentAt = new(2026, 8, 8, 11, 15, 0, DateTimeKind.Utc);

    private const string Body = "my geyser is still leaking, ring the doorbell twice";

    private static string ExpectedGroup => ChatGroups.Thread(ThreadId);

    private static (ChatHubBroadcastHandler Handler, RecordingHubContext Hub, RecordingLogger<ChatHubBroadcastHandler> Logger) Build()
    {
        var hub = new RecordingHubContext();
        var logger = new RecordingLogger<ChatHubBroadcastHandler>();
        return (new ChatHubBroadcastHandler(hub, logger), hub, logger);
    }

    private static DomainEventNotification<ChatMessageSentEvent> MessageSent(Guid? threadId = null) =>
        new(new ChatMessageSentEvent(
            MessageId, threadId ?? ThreadId, ChatContextType.Booking, ContextId,
            SenderId, ChatSenderType.Admin, Body, SentAt));

    // --- The happy path is unchanged by the guard ---

    [Fact]
    public async Task A_persisted_message_is_pushed_to_its_thread_group_as_MessageReceived()
    {
        var (handler, hub, _) = Build();

        await handler.Handle(MessageSent(), CancellationToken.None);

        var frame = hub.Frames.Should().ContainSingle().Subject;
        frame.Group.Should().Be(ExpectedGroup);
        frame.Method.Should().Be("MessageReceived");
        frame.Payload.Should().BeEquivalentTo(
            new ChatMessageResponse(MessageId, ThreadId, SenderId, ChatSenderType.Admin, Body, SentAt, ReadAtUtc: null));
    }

    /// <summary>
    /// The group name is the hub's, not one this handler invents - if the two
    /// ever disagree the hub joins connections to a group nothing broadcasts
    /// to and chat goes silent with the test above still green.
    /// </summary>
    [Fact]
    public async Task The_group_is_the_one_the_hub_joins_connections_to()
    {
        var (handler, hub, _) = Build();

        await handler.Handle(MessageSent(), CancellationToken.None);

        hub.Frames.Should().ContainSingle()
            .Which.Group.Should().Be($"chat-thread-{ThreadId:D}");
    }

    [Fact]
    public async Task Broadcasts_go_to_the_thread_that_raised_the_event_and_no_other()
    {
        var (handler, hub, _) = Build();
        var otherThread = Guid.Parse("88888888-8888-8888-8888-888888888888");

        await handler.Handle(MessageSent(otherThread), CancellationToken.None);

        hub.Frames.Should().ContainSingle()
            .Which.Group.Should().Be(ChatGroups.Thread(otherThread))
            .And.NotBe(ExpectedGroup);
    }

    // --- A failed broadcast must never break the request that caused it ---

    /// <summary>
    /// The point of task 292. Dispatch is post-commit
    /// (docs/ARCHITECTURE.md, "DOMAIN EVENT DISPATCH AND DELIVERY"), so an
    /// exception out of here surfaces to the caller as a failed request whose
    /// message row was already written: the customer sees an error, retries,
    /// and the thread ends up with the message twice.
    /// </summary>
    [Fact]
    public async Task A_throwing_hub_context_does_not_propagate_out_of_the_handler()
    {
        var (handler, hub, _) = Build();
        hub.ThrowOnSend = new InvalidOperationException("No connection is available to service this operation.");

        var broadcast = async () => await handler.Handle(MessageSent(), CancellationToken.None);

        await broadcast.Should().NotThrowAsync();
    }

    [Fact]
    public async Task A_swallowed_broadcast_failure_is_logged_as_a_warning_naming_the_thread()
    {
        var (handler, hub, logger) = Build();
        hub.ThrowOnSend = new InvalidOperationException("No connection is available to service this operation.");

        await handler.Handle(MessageSent(), CancellationToken.None);

        var warning = logger.At(LogLevel.Warning).Should().ContainSingle().Subject;
        warning.Message.Should().Contain(ThreadId.ToString());
        warning.Exception.Should().BeSameAs(hub.ThrowOnSend);
    }

    /// <summary>
    /// Swallowing must not become "swallow and log the payload". The body is
    /// customer-authored free text - an address, a phone number and a gate
    /// code all routinely arrive in one - and the log stream is the easiest
    /// place for a payload's contents to end up
    /// (docs/CODING-STANDARDS.md LOGGING).
    /// </summary>
    [Fact]
    public async Task A_swallowed_failure_never_logs_the_message_body()
    {
        var (handler, hub, logger) = Build();
        hub.ThrowOnSend = new InvalidOperationException("boom");

        await handler.Handle(MessageSent(), CancellationToken.None);

        logger.Text.Should().NotContain("geyser", "the message body must never reach the log stream");
    }

    /// <summary>
    /// The one carve-out. Cancellation is the caller's decision, not a
    /// broadcast failure; swallowing it would turn a cooperative cancellation
    /// into a silent one and leave a shutting-down process reporting success.
    /// </summary>
    [Fact]
    public async Task Cancellation_is_not_swallowed()
    {
        var (handler, hub, logger) = Build();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        hub.ThrowOnSend = new OperationCanceledException(cts.Token);

        var broadcast = async () => await handler.Handle(MessageSent(), cts.Token);

        await broadcast.Should().ThrowAsync<OperationCanceledException>();
        logger.Entries.Should().BeEmpty();
    }

    /// <summary>
    /// The carve-out is scoped to the caller's token, not to the exception
    /// type: SignalR's Redis backplane can surface a
    /// <see cref="TaskCanceledException"/> from its own internal timeout with
    /// nobody having cancelled anything, and that is an infrastructure failure
    /// like any other. It must be swallowed, not re-thrown at a caller who
    /// never asked to stop - otherwise a backplane timeout is exactly the
    /// duplicate-message bug this task removes.
    /// </summary>
    [Fact]
    public async Task A_cancellation_nobody_asked_for_is_still_swallowed()
    {
        var (handler, hub, logger) = Build();
        hub.ThrowOnSend = new TaskCanceledException("The operation timed out.");

        var broadcast = async () => await handler.Handle(MessageSent(), CancellationToken.None);

        await broadcast.Should().NotThrowAsync();
        logger.At(LogLevel.Warning).Should().ContainSingle();
    }

    // --- The fake ---

    private sealed record SentFrame(string Group, string Method, object? Payload);

    /// <summary>
    /// Records what would have gone on the wire. Only <c>Group</c> is
    /// implemented; every other <see cref="IHubClients"/> member throws, so
    /// broadcasting a private thread's message to <c>All</c> or to a bare user
    /// id fails the suite instead of silently over-sharing.
    /// </summary>
    private sealed class RecordingHubContext : IHubContext<ChatHub>
    {
        public List<SentFrame> Frames { get; } = [];

        /// <summary>Set to make every send fail, standing in for Redis being unreachable.</summary>
        public Exception? ThrowOnSend { get; set; }

        public IHubClients Clients => new RecordingClients(this);

        public IGroupManager Groups =>
            throw new NotSupportedException("A broadcast handler manages no group membership; the hub does.");

        private sealed class RecordingClients(RecordingHubContext owner) : IHubClients
        {
            public IClientProxy Group(string groupName) => new RecordingClientProxy(owner, groupName);

            public IClientProxy All => throw Refuse(nameof(All));

            public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => throw Refuse(nameof(AllExcept));

            public IClientProxy Client(string connectionId) => throw Refuse(nameof(Client));

            public IClientProxy Clients(IReadOnlyList<string> connectionIds) => throw Refuse(nameof(Clients));

            public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) =>
                throw Refuse(nameof(GroupExcept));

            public IClientProxy Groups(IReadOnlyList<string> groupNames) => throw Refuse(nameof(Groups));

            public IClientProxy User(string userId) => throw Refuse(nameof(User));

            public IClientProxy Users(IReadOnlyList<string> userIds) => throw Refuse(nameof(Users));

            private static NotSupportedException Refuse(string member) => new(
                $"A chat broadcast addresses exactly one thread group; IHubClients.{member} would reach connections that never joined it.");
        }

        private sealed class RecordingClientProxy(RecordingHubContext owner, string groupName) : IClientProxy
        {
            public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
            {
                if (owner.ThrowOnSend is { } failure)
                {
                    return Task.FromException(failure);
                }

                owner.Frames.Add(new SentFrame(groupName, method, args.SingleOrDefault()));
                return Task.CompletedTask;
            }
        }
    }
}

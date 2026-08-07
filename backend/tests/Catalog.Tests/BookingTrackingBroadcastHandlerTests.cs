using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Nestly.Application.Tracking;
using Nestly.BuildingBlocks.Primitives;
using Nestly.Domain;
using Nestly.Domain.Events;
using Nestly.Infrastructure.Persistence.Interceptors;
using Nestly.Infrastructure.Realtime;

namespace Nestly.Catalog.Tests;

/// <summary>
/// Task 274: the three tracking broadcasts. Everything here runs against
/// <see cref="RecordingHubContext"/> - a hand-rolled
/// <see cref="IHubContext{THub}"/> that records frames instead of sending
/// them, so no test opens a socket or reaches Redis. That fake is also
/// deliberately hostile: every <see cref="IHubClients"/> member other than
/// <c>Group</c> throws, so a handler that broadcast to <c>All</c> - putting one
/// booking's provider coordinates on every live connection in the process -
/// fails loudly here rather than shipping.
/// </summary>
public sealed class BookingTrackingBroadcastHandlerTests
{
    private static readonly Guid BookingId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ProviderId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTime RecordedAt = new(2026, 8, 7, 9, 30, 0, DateTimeKind.Utc);

    private static string ExpectedGroup => TrackingGroups.Booking(BookingId);

    private static (BookingTrackingBroadcastHandler Handler, RecordingHubContext Hub, RecordingLogger<BookingTrackingBroadcastHandler> Logger) Build()
    {
        var hub = new RecordingHubContext();
        var logger = new RecordingLogger<BookingTrackingBroadcastHandler>();
        return (new BookingTrackingBroadcastHandler(hub, logger), hub, logger);
    }

    private static DomainEventNotification<BookingStatusChangedEvent> StatusChanged(
        BookingStatus from = BookingStatus.Assigned, BookingStatus to = BookingStatus.ProviderEnRoute) =>
        new(new BookingStatusChangedEvent(BookingId, from, to));

    private static DomainEventNotification<ProviderLocationUpdatedEvent> LocationUpdated(Guid? bookingId) =>
        new(new ProviderLocationUpdatedEvent(
            Guid.NewGuid(), ProviderId, bookingId, 12.9716m, 77.5946m, 8.5m, RecordedAt));

    private static DomainEventNotification<BookingEtaUpdatedEvent> EtaUpdated() =>
        new(new BookingEtaUpdatedEvent(BookingId, ProviderId, 540, RecordedAt));

    // --- Each event reaches the booking's group under the agreed method name ---

    [Fact]
    public async Task A_status_change_is_pushed_to_the_booking_group_as_BookingStatusChanged()
    {
        var (handler, hub, _) = Build();

        await handler.Handle(StatusChanged(), CancellationToken.None);

        var frame = hub.Frames.Should().ContainSingle().Subject;
        frame.Group.Should().Be(ExpectedGroup);
        frame.Method.Should().Be("BookingStatusChanged");
    }

    [Fact]
    public async Task A_location_fix_is_pushed_to_the_booking_group_as_ProviderLocationUpdated()
    {
        var (handler, hub, _) = Build();

        await handler.Handle(LocationUpdated(BookingId), CancellationToken.None);

        var frame = hub.Frames.Should().ContainSingle().Subject;
        frame.Group.Should().Be(ExpectedGroup);
        frame.Method.Should().Be("ProviderLocationUpdated");
    }

    [Fact]
    public async Task An_eta_recomputation_is_pushed_to_the_booking_group_as_EtaUpdated()
    {
        var (handler, hub, _) = Build();

        await handler.Handle(EtaUpdated(), CancellationToken.None);

        var frame = hub.Frames.Should().ContainSingle().Subject;
        frame.Group.Should().Be(ExpectedGroup);
        frame.Method.Should().Be("EtaUpdated");
    }

    /// <summary>
    /// The group name is task 273's, not one this handler invents - if the two
    /// ever disagree the hub joins connections to a group nothing broadcasts to
    /// and the whole feature goes silent with every test above still green,
    /// because they would all be asserting against the same wrong constant.
    /// </summary>
    [Fact]
    public async Task The_group_is_the_one_the_hub_joins_connections_to()
    {
        var (handler, hub, _) = Build();

        await handler.Handle(StatusChanged(), CancellationToken.None);

        hub.Frames.Should().ContainSingle()
            .Which.Group.Should().Be($"booking-tracking-{BookingId:D}");
    }

    [Fact]
    public async Task Broadcasts_go_to_the_booking_that_raised_the_event_and_no_other()
    {
        var (handler, hub, _) = Build();
        var otherBooking = Guid.Parse("33333333-3333-3333-3333-333333333333");

        await handler.Handle(
            new DomainEventNotification<BookingEtaUpdatedEvent>(
                new BookingEtaUpdatedEvent(otherBooking, ProviderId, 60, RecordedAt)),
            CancellationToken.None);

        hub.Frames.Should().ContainSingle()
            .Which.Group.Should().Be(TrackingGroups.Booking(otherBooking))
            .And.NotBe(ExpectedGroup);
    }

    // --- Payload shape ---

    [Fact]
    public async Task The_status_payload_carries_both_ends_of_the_transition()
    {
        var (handler, hub, _) = Build();

        await handler.Handle(
            StatusChanged(BookingStatus.ProviderEnRoute, BookingStatus.ProviderArrived),
            CancellationToken.None);

        hub.Frames.Should().ContainSingle().Which.Payload.Should().BeEquivalentTo(
            new BookingStatusChangedBroadcast(
                BookingId, BookingStatus.ProviderEnRoute, BookingStatus.ProviderArrived));
    }

    /// <summary>
    /// Lat/lng/recordedAt and the booking id - and specifically NOT the
    /// provider id, the ping id or the accuracy that
    /// <see cref="ProviderLocationUpdatedEvent"/> also carries. Asserted as a
    /// whole-object equivalence, so a field added to the broadcast record
    /// fails here as well as in the reflection guard below.
    /// </summary>
    [Fact]
    public async Task The_location_payload_carries_the_fix_and_nothing_else()
    {
        var (handler, hub, _) = Build();

        await handler.Handle(LocationUpdated(BookingId), CancellationToken.None);

        hub.Frames.Should().ContainSingle().Which.Payload.Should().BeOfType<ProviderLocationBroadcast>()
            .Which.Should().Be(new ProviderLocationBroadcast(BookingId, 12.9716m, 77.5946m, RecordedAt));
    }

    [Fact]
    public async Task The_eta_payload_carries_the_number_and_when_it_was_computed()
    {
        var (handler, hub, _) = Build();

        await handler.Handle(EtaUpdated(), CancellationToken.None);

        hub.Frames.Should().ContainSingle().Which.Payload.Should().BeEquivalentTo(
            new BookingEtaBroadcast(BookingId, 540, RecordedAt));
    }

    /// <summary>
    /// The domain events themselves must never go on the wire - they are
    /// in-process types free to grow fields for in-process consumers, and
    /// serialising one directly would put <c>ProviderId</c> and <c>PingId</c>
    /// in a browser today and whatever task 275+ adds tomorrow.
    /// </summary>
    [Fact]
    public async Task No_domain_event_is_ever_the_payload()
    {
        var (handler, hub, _) = Build();

        await handler.Handle(StatusChanged(), CancellationToken.None);
        await handler.Handle(LocationUpdated(BookingId), CancellationToken.None);
        await handler.Handle(EtaUpdated(), CancellationToken.None);

        hub.Frames.Should().HaveCount(3);
        hub.Frames.Select(frame => frame.Payload).Should().AllSatisfy(
            payload => payload.Should().NotBeAssignableTo<IDomainEvent>());
    }

    // --- The PII rule: the hard constraint of this task ---

    /// <summary>
    /// Same reflection guard task 272 put on the events themselves
    /// (<c>BookingTrackingEventsTests.No_tracking_event_payload_carries_free_text</c>),
    /// applied one layer out where it actually matters: these records are the
    /// wire format, so anything on one is in a browser. A name, a mobile
    /// number, an email and an address are all strings, so the absence of
    /// strings is the cheap structural guard - a reviewer adding
    /// <c>ProviderName</c> or <c>CustomerMobile</c> to a broadcast fails here
    /// rather than in a privacy review after launch.
    /// </summary>
    [Theory]
    [InlineData(typeof(BookingStatusChangedBroadcast))]
    [InlineData(typeof(ProviderLocationBroadcast))]
    [InlineData(typeof(BookingEtaBroadcast))]
    public void No_broadcast_payload_carries_free_text(Type payloadType)
    {
        var textProperties = payloadType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.PropertyType == typeof(string))
            .Select(property => property.Name);

        textProperties.Should().BeEmpty();
    }

    /// <summary>
    /// The stricter half of the guard: an exact allow-list of property names
    /// per payload, because not every way to leak is a string. Re-adding
    /// <c>ProviderId</c> (a handle other endpoints resolve to a person), or
    /// attaching a <c>CustomerSnapshot</c>, or bolting a masked-phone value
    /// object on for task 275's convenience, are all non-string widenings that
    /// <see cref="No_broadcast_payload_carries_free_text"/> would wave through.
    /// Widening the wire has to be a deliberate edit to this list.
    /// </summary>
    [Theory]
    [InlineData(typeof(BookingStatusChangedBroadcast), "BookingId,FromStatus,ToStatus")]
    [InlineData(typeof(ProviderLocationBroadcast), "BookingId,Latitude,Longitude,RecordedAtUtc")]
    [InlineData(typeof(BookingEtaBroadcast), "BookingId,EtaSeconds,EtaComputedAtUtc")]
    public void A_broadcast_payload_carries_exactly_its_agreed_fields(Type payloadType, string expected)
    {
        var actual = payloadType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name);

        actual.Should().BeEquivalentTo(expected.Split(','));
    }

    /// <summary>
    /// An idle/on-shift fix has no booking (task 272) and belongs to no
    /// tracking group. Broadcasting it would mean inventing a group name -
    /// either from the provider id, putting a provider's off-job movements on
    /// a channel a customer can join, or from <see cref="Guid.Empty"/>, which
    /// pools every provider's idle trail into one group anybody could guess.
    /// </summary>
    [Fact]
    public async Task A_fix_with_no_booking_is_broadcast_nowhere()
    {
        var (handler, hub, _) = Build();

        await handler.Handle(LocationUpdated(bookingId: null), CancellationToken.None);

        hub.Frames.Should().BeEmpty();
    }

    // --- A failed broadcast must never break the request that caused it ---

    /// <summary>
    /// Dispatch is post-commit (docs/ARCHITECTURE.md), so an exception out of
    /// here surfaces to the caller as a failed request whose data was already
    /// written - a provider's location ping rejected because Redis blinked.
    /// </summary>
    [Theory]
    [InlineData("status")]
    [InlineData("location")]
    [InlineData("eta")]
    public async Task A_throwing_hub_context_does_not_propagate_out_of_the_handler(string which)
    {
        var (handler, hub, _) = Build();
        hub.ThrowOnSend = new InvalidOperationException("No connection is available to service this operation.");

        var broadcast = async () => await (which switch
        {
            "status" => handler.Handle(StatusChanged(), CancellationToken.None),
            "location" => handler.Handle(LocationUpdated(BookingId), CancellationToken.None),
            _ => handler.Handle(EtaUpdated(), CancellationToken.None),
        });

        await broadcast.Should().NotThrowAsync();
    }

    [Fact]
    public async Task A_swallowed_broadcast_failure_is_logged_as_a_warning_naming_the_booking()
    {
        var (handler, hub, logger) = Build();
        hub.ThrowOnSend = new InvalidOperationException("No connection is available to service this operation.");

        await handler.Handle(LocationUpdated(BookingId), CancellationToken.None);

        var warning = logger.At(LogLevel.Warning).Should().ContainSingle().Subject;
        warning.Message.Should().Contain(BookingId.ToString());
        warning.Exception.Should().BeSameAs(hub.ThrowOnSend);
    }

    /// <summary>
    /// Swallowing must not become "swallow and log the payload". Coordinates
    /// are not PII on their own, but they are the shape of the thing this
    /// feature must never spill, and the log stream is the easiest place for a
    /// future payload's contents to end up (docs/CODING-STANDARDS.md LOGGING).
    /// </summary>
    [Fact]
    public async Task A_swallowed_failure_does_not_log_the_payload()
    {
        var (handler, hub, logger) = Build();
        hub.ThrowOnSend = new InvalidOperationException("boom");

        await handler.Handle(LocationUpdated(BookingId), CancellationToken.None);

        logger.Text.Should().NotContain("12.9716").And.NotContain("77.5946");
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

        var broadcast = async () => await handler.Handle(StatusChanged(), cts.Token);

        await broadcast.Should().ThrowAsync<OperationCanceledException>();
        logger.Entries.Should().BeEmpty();
    }

    /// <summary>
    /// The cancellation carve-out is scoped to the caller's token, not to the
    /// exception type: SignalR's Redis backplane can surface a
    /// <see cref="TaskCanceledException"/> from its own internal timeout with
    /// nobody having cancelled anything, and that is an infrastructure failure
    /// like any other. It must be swallowed, not re-thrown at a caller who
    /// never asked to stop.
    /// </summary>
    [Fact]
    public async Task A_cancellation_nobody_asked_for_is_still_swallowed()
    {
        var (handler, hub, logger) = Build();
        hub.ThrowOnSend = new TaskCanceledException("The operation timed out.");

        var broadcast = async () => await handler.Handle(StatusChanged(), CancellationToken.None);

        await broadcast.Should().NotThrowAsync();
        logger.At(LogLevel.Warning).Should().ContainSingle();
    }

    // --- The fake ---

    private sealed record SentFrame(string Group, string Method, object? Payload);

    /// <summary>
    /// Records what would have gone on the wire. Only <c>Group</c> is
    /// implemented; every other <see cref="IHubClients"/> member throws, so
    /// broadcasting a booking's tracking data to <c>All</c> or to a bare user
    /// id fails the suite instead of silently over-sharing.
    /// </summary>
    private sealed class RecordingHubContext : IHubContext<BookingTrackingHub>
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
                $"Tracking broadcasts address exactly one booking group; IHubClients.{member} would reach connections that never joined it.");
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

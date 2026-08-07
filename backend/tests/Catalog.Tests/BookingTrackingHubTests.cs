using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Nestly.Application;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence;
using Nestly.Infrastructure.Persistence.Repositories;
using Nestly.Infrastructure.Realtime;

namespace Nestly.Catalog.Tests;

/// <summary>
/// Task 278: drives <see cref="BookingTrackingHub"/> itself, not just the
/// <see cref="BookingTrackingAuthorizer"/> it delegates to
/// (<see cref="BookingTrackingAuthorizerTests"/> already covers every actor/
/// status combination for the authorizer in isolation). What is untested
/// without this file is the wiring: that a denied <c>JoinBooking</c> call
/// never reaches <see cref="IGroupManager.AddToGroupAsync"/> - i.e. that
/// knowing a booking id's GUID is not, by itself, enough to end up in its
/// tracking group, and that the denial carries no information (message,
/// timing, or otherwise) that would let a caller distinguish "wrong booking"
/// from "not yours" from "no longer trackable" (see
/// <see cref="BookingTrackingHub.AccessDeniedMessage"/>'s doc comment).
/// </summary>
public sealed class BookingTrackingHubTests : IClassFixture<TestDatabase>
{
    private readonly TestDatabase _db;

    public BookingTrackingHubTests(TestDatabase db) => _db = db;

    private sealed class FakeGroupManager : IGroupManager
    {
        public List<(string ConnectionId, string GroupName)> Added { get; } = [];
        public List<(string ConnectionId, string GroupName)> Removed { get; } = [];

        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            Added.Add((connectionId, groupName));
            return Task.CompletedTask;
        }

        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            Removed.Add((connectionId, groupName));
            return Task.CompletedTask;
        }
    }

    /// <summary>The minimum a hub method touches: connection id and the authenticated principal. Every other member is unused by <see cref="BookingTrackingHub"/> and throws if that ever changes.</summary>
    private sealed class FakeHubCallerContext : HubCallerContext
    {
        public FakeHubCallerContext(string connectionId, ClaimsPrincipal? user)
        {
            ConnectionId = connectionId;
            User = user;
        }

        public override string ConnectionId { get; }
        public override string? UserIdentifier => User?.FindFirstValue(JwtRegisteredClaimNames.Sub);
        public override ClaimsPrincipal? User { get; }
        public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
        public override IFeatureCollection Features { get; } = new FeatureCollection();
        public override CancellationToken ConnectionAborted => CancellationToken.None;

        public override void Abort() => throw new NotSupportedException("Not exercised by these tests.");
    }

    private static BookingTrackingHub Hub(NestlyDbContext context, RealtimeActorKind kind, ClaimsPrincipal? user, string connectionId, out FakeGroupManager groups)
    {
        var authorizer = new BookingTrackingAuthorizer(
            new RealtimeActorContext(kind), new BookingRepository(context), new BookingProviderAssignmentRepository(context));
        groups = new FakeGroupManager();
        return new BookingTrackingHub(authorizer, new RecordingLogger<BookingTrackingHub>())
        {
            Context = new FakeHubCallerContext(connectionId, user),
            Groups = groups
        };
    }

    private static ClaimsPrincipal Principal(Guid subjectId) =>
        new(new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Sub, subjectId.ToString())], "TestJwt"));

    private static Booking SeedTrackableBooking(NestlyDbContext context, Guid customerId)
    {
        var customer = new Customer(customerId, "9" + Guid.NewGuid().ToString("N")[..9], "Test Customer", CustomerStatus.Active);
        context.Add(customer);

        var booking = new Booking(
            Guid.NewGuid(), customerId,
            new CustomerSnapshot("Test Customer", customer.Mobile),
            null,
            new AddressSnapshot("Home", "123 St", null, null, "560001", "Bengaluru", "Karnataka", 12.9m, 77.5m, "Test", "9000000000"),
            new SlotSnapshot(Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow), "Morning", TimeSpan.FromHours(9), TimeSpan.FromHours(13)),
            new PriceSnapshot(500m, 1, 500m, 0, 0, 500m, 0, 0, 0, 500m));

        foreach (var step in new[] { BookingStatus.PaymentPending, BookingStatus.Confirmed, BookingStatus.AwaitingFulfilment, BookingStatus.Assigned })
        {
            booking.TransitionTo(step, "test");
        }

        context.Add(booking);
        context.SaveChanges();
        return booking;
    }

    [Fact]
    public async Task JoinBooking_adds_the_owning_customers_connection_to_the_bookings_group()
    {
        using var context = _db.CreateContext();
        var customerId = Guid.NewGuid();
        var booking = SeedTrackableBooking(context, customerId);

        var hub = Hub(context, RealtimeActorKind.Customer, Principal(customerId), "conn-1", out var groups);

        await hub.JoinBooking(booking.Id);

        groups.Added.Should().ContainSingle(g => g.ConnectionId == "conn-1" && g.GroupName == TrackingGroups.Booking(booking.Id));
    }

    /// <summary>
    /// The core of task 278 item 6: guessing another customer's booking id
    /// must not put this connection in that booking's group. A regression
    /// here (e.g. someone moving the AddToGroupAsync call above the
    /// authorization check) would let any authenticated connection watch any
    /// booking's live location by GUID alone.
    /// </summary>
    [Fact]
    public async Task JoinBooking_never_adds_the_connection_to_the_group_when_the_caller_does_not_own_the_booking()
    {
        using var context = _db.CreateContext();
        var ownerId = Guid.NewGuid();
        var strangerId = Guid.NewGuid();
        var booking = SeedTrackableBooking(context, ownerId);

        var hub = Hub(context, RealtimeActorKind.Customer, Principal(strangerId), "conn-2", out var groups);

        var act = () => hub.JoinBooking(booking.Id);

        await act.Should().ThrowAsync<HubException>().WithMessage(BookingTrackingHub.AccessDeniedMessage);
        groups.Added.Should().BeEmpty("an unauthorized join must never reach the group manager, guessed booking id or not");
    }

    [Fact]
    public async Task JoinBooking_never_adds_the_connection_to_the_group_for_a_booking_id_that_does_not_exist()
    {
        using var context = _db.CreateContext();
        var hub = Hub(context, RealtimeActorKind.Customer, Principal(Guid.NewGuid()), "conn-3", out var groups);

        var act = () => hub.JoinBooking(Guid.NewGuid());

        var thrown = await act.Should().ThrowAsync<HubException>();
        thrown.Which.Message.Should().Be(BookingTrackingHub.AccessDeniedMessage,
            "a nonexistent booking id must be indistinguishable from one that exists but is not the caller's");
        groups.Added.Should().BeEmpty();
    }

    /// <summary>
    /// The denial log line is the only diagnostic a denied join produces;
    /// task 278 item 7 requires that it carries no claim values, no name and
    /// no mobile/coordinate data - only the booking id and connection id, per
    /// the comment on <see cref="BookingTrackingHub.JoinBooking"/>.
    /// </summary>
    [Fact]
    public async Task JoinBooking_denial_logs_only_the_booking_and_connection_id_never_the_callers_claims()
    {
        using var context = _db.CreateContext();
        var ownerId = Guid.NewGuid();
        var strangerId = Guid.NewGuid();
        var booking = SeedTrackableBooking(context, ownerId);

        var authorizer = new BookingTrackingAuthorizer(
            new RealtimeActorContext(RealtimeActorKind.Customer), new BookingRepository(context), new BookingProviderAssignmentRepository(context));
        var logger = new RecordingLogger<BookingTrackingHub>();
        var hub = new BookingTrackingHub(authorizer, logger)
        {
            Context = new FakeHubCallerContext("conn-4", Principal(strangerId)),
            Groups = new FakeGroupManager()
        };

        await hub.Invoking(h => h.JoinBooking(booking.Id)).Should().ThrowAsync<HubException>();

        logger.Text.Should().Contain(booking.Id.ToString());
        logger.Text.Should().Contain("conn-4");
        logger.Text.Should().NotContain(strangerId.ToString(), "the subject claim must not be logged, only the booking and connection id");
        logger.Text.Should().NotContain(ownerId.ToString());
    }

    [Fact]
    public async Task LeaveBooking_removes_the_connection_from_the_group_without_an_authorization_check()
    {
        using var context = _db.CreateContext();
        var hub = Hub(context, RealtimeActorKind.Customer, Principal(Guid.NewGuid()), "conn-5", out var groups);
        var bookingId = Guid.NewGuid();

        await hub.LeaveBooking(bookingId);

        groups.Removed.Should().ContainSingle(g => g.ConnectionId == "conn-5" && g.GroupName == TrackingGroups.Booking(bookingId));
    }
}

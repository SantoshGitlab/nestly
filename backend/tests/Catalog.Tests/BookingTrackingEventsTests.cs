using System.Reflection;
using FluentAssertions;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nestly.Application;
using Nestly.BuildingBlocks.Primitives;
using Nestly.Domain;
using Nestly.Domain.Events;
using Nestly.Infrastructure.Persistence;
using Nestly.Infrastructure.Persistence.Interceptors;

namespace Nestly.Catalog.Tests;

/// <summary>
/// Task 272: the order-tracking domain events and the raise sites that produce
/// them. Two things are being pinned here, and they fail independently.
///
/// First, that each event is raised at all - before this task
/// <see cref="BookingProviderAssignment.Accept"/> raised nothing, so a provider
/// accepting a job was invisible to the rest of the system.
///
/// Second, that a raised event can actually be *dispatched*.
/// <c>DomainEventDispatchInterceptor</c> only sweeps
/// <c>ChangeTracker.Entries&lt;AggregateRoot&lt;Guid&gt;&gt;()</c>, so an event
/// raised on a plain <see cref="Entity{TId}"/> is collected, saved and silently
/// dropped - a failure mode that is invisible to an assertion on
/// <c>DomainEvents</c> alone. The dispatch tests below therefore go through a
/// real SaveChanges, and would fail if <see cref="BookingProviderAssignment"/>
/// or <see cref="ProviderLocationPing"/> were demoted back to entities.
/// </summary>
public sealed class BookingTrackingEventsTests
{
    private static readonly AddressSnapshot Address = new(
        "Home", "221B Baker Street", null, "Near the park", "560001", "Bengaluru", "Karnataka", 12.9716m, 77.5946m, "Asha Rao", "9876543210");

    private static readonly SlotSnapshot Slot = new(
        Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)), "Morning", TimeSpan.FromHours(9), TimeSpan.FromHours(13));

    private static readonly PriceSnapshot Price = new(
        BasePrice: 500m, Quantity: 1, BaseTotal: 500m, AddOnTotal: 0m, VisitCharge: 50m,
        Subtotal: 550m, TaxPercentage: 18m, TaxAmount: 99m, PlatformFee: 10m, TotalPayable: 659m);

    private static Booking NewBooking() =>
        new(Guid.NewGuid(), Guid.NewGuid(), new CustomerSnapshot("Asha Rao", "9876543210"), null, Address, Slot, Price);

    /// <summary>A booking walked to Assigned with a provider on it - the only state the tracking transitions are reachable from.</summary>
    private static Booking BookingAssignedTo(Guid providerId)
    {
        var booking = NewBooking();
        booking.TransitionTo(BookingStatus.PaymentPending);
        booking.TransitionTo(BookingStatus.Confirmed);
        booking.TransitionTo(BookingStatus.AwaitingFulfilment);
        booking.TransitionTo(BookingStatus.Assigned);
        booking.AssignProvider(providerId);
        return booking;
    }

    private static BookingProviderAssignment NewAssignment(Guid bookingId, Guid providerId) =>
        new(Guid.NewGuid(), bookingId, providerId, BookingAssignedByType.Admin, Guid.NewGuid(), DateTime.UtcNow.AddHours(2));

    // --- ProviderAssignmentAcceptedEvent ---

    [Fact]
    public void Accepting_an_assignment_raises_ProviderAssignmentAcceptedEvent_carrying_the_assignment_booking_and_provider()
    {
        var bookingId = Guid.NewGuid();
        var providerId = Guid.NewGuid();
        var assignment = NewAssignment(bookingId, providerId);

        assignment.Accept();

        var raised = assignment.DomainEvents.OfType<ProviderAssignmentAcceptedEvent>().Should().ContainSingle().Subject;
        raised.AssignmentId.Should().Be(assignment.Id);
        raised.BookingId.Should().Be(bookingId);
        raised.ProviderId.Should().Be(providerId);
        raised.AcceptedAtUtc.Should().Be(assignment.RespondedAt!.Value);
    }

    [Fact]
    public void An_assignment_that_was_never_accepted_raises_nothing()
    {
        var assignment = NewAssignment(Guid.NewGuid(), Guid.NewGuid());

        assignment.DomainEvents.Should().BeEmpty();
    }

    /// <summary>
    /// The other three responses raise no acceptance event. Accept is the only
    /// one that means "this job is now trackable"; a rejected, superseded or
    /// withdrawn assignment must not put a customer on a tracking screen.
    /// </summary>
    /// <remarks>
    /// Superseding does raise an event of its own since task 295
    /// (<c>BookingProviderChangedEvent</c> - covered in
    /// <c>ProviderReassignmentNotificationTests</c>), which is why this case
    /// asserts on the acceptance event's absence rather than on an empty list.
    /// </remarks>
    [Fact]
    public void Rejecting_superseding_or_withdrawing_an_assignment_raises_no_acceptance_event()
    {
        var rejected = NewAssignment(Guid.NewGuid(), Guid.NewGuid());
        rejected.Reject("Too far.");

        var reassigned = NewAssignment(Guid.NewGuid(), Guid.NewGuid());
        reassigned.MarkReassigned(Guid.NewGuid());

        var withdrawn = NewAssignment(Guid.NewGuid(), Guid.NewGuid());
        withdrawn.Withdraw();

        rejected.DomainEvents.Should().BeEmpty();
        reassigned.DomainEvents.OfType<ProviderAssignmentAcceptedEvent>().Should().BeEmpty();
        withdrawn.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void A_second_Accept_is_still_rejected_and_raises_no_second_event()
    {
        var assignment = NewAssignment(Guid.NewGuid(), Guid.NewGuid());
        assignment.Accept();

        var acceptAgain = () => assignment.Accept();

        acceptAgain.Should().Throw<InvalidOperationException>();
        assignment.DomainEvents.OfType<ProviderAssignmentAcceptedEvent>().Should().ContainSingle();
    }

    // --- ProviderEnRouteEvent / ProviderArrivedEvent ---

    [Fact]
    public void Transitioning_to_ProviderEnRoute_raises_ProviderEnRouteEvent_with_the_assigned_provider()
    {
        var providerId = Guid.NewGuid();
        var booking = BookingAssignedTo(providerId);
        booking.ClearDomainEvents();

        booking.TransitionTo(BookingStatus.ProviderEnRoute, "Provider set off.");

        var raised = booking.DomainEvents.OfType<ProviderEnRouteEvent>().Should().ContainSingle().Subject;
        raised.BookingId.Should().Be(booking.Id);
        raised.ProviderId.Should().Be(providerId);
    }

    [Fact]
    public void Transitioning_to_ProviderArrived_raises_ProviderArrivedEvent_with_the_assigned_provider()
    {
        var providerId = Guid.NewGuid();
        var booking = BookingAssignedTo(providerId);
        booking.TransitionTo(BookingStatus.ProviderEnRoute);
        booking.ClearDomainEvents();

        booking.TransitionTo(BookingStatus.ProviderArrived, "Provider reached the address.");

        var raised = booking.DomainEvents.OfType<ProviderArrivedEvent>().Should().ContainSingle().Subject;
        raised.BookingId.Should().Be(booking.Id);
        raised.ProviderId.Should().Be(providerId);
    }

    /// <summary>
    /// The tracking events are companions to <see cref="BookingStatusChangedEvent"/>,
    /// not replacements - the existing lifecycle-wide handlers (metrics, escrow,
    /// referrals, coins) all subscribe to that stream and must keep seeing these
    /// two transitions.
    /// </summary>
    [Fact]
    public void The_tracking_transitions_still_raise_BookingStatusChangedEvent_as_well()
    {
        var booking = BookingAssignedTo(Guid.NewGuid());
        booking.ClearDomainEvents();

        booking.TransitionTo(BookingStatus.ProviderEnRoute);

        var statusChanged = booking.DomainEvents.OfType<BookingStatusChangedEvent>().Should().ContainSingle().Subject;
        statusChanged.FromStatus.Should().Be(BookingStatus.Assigned);
        statusChanged.ToStatus.Should().Be(BookingStatus.ProviderEnRoute);
        booking.DomainEvents.Should().HaveCount(2, "exactly one tracking event accompanies the status change, never more");
    }

    /// <summary>
    /// The regression that matters most: a provider who skips the optional
    /// tracking taps (Assigned -&gt; InProgress, still legal per task 264) must
    /// not produce an en-route or arrived event that never happened.
    /// </summary>
    [Theory]
    [InlineData(BookingStatus.InProgress)]
    [InlineData(BookingStatus.CancelledByCustomer)]
    [InlineData(BookingStatus.Rescheduled)]
    public void Transitions_other_than_the_two_tracking_states_raise_no_tracking_event(BookingStatus target)
    {
        var booking = BookingAssignedTo(Guid.NewGuid());
        booking.ClearDomainEvents();

        booking.TransitionTo(target);

        booking.DomainEvents.OfType<ProviderEnRouteEvent>().Should().BeEmpty();
        booking.DomainEvents.OfType<ProviderArrivedEvent>().Should().BeEmpty();
    }

    [Fact]
    public void A_rejected_transition_into_a_tracking_state_raises_nothing()
    {
        var booking = NewBooking();
        booking.ClearDomainEvents();

        var illegal = () => booking.TransitionTo(BookingStatus.ProviderEnRoute);

        illegal.Should().Throw<InvalidOperationException>();
        booking.DomainEvents.Should().BeEmpty();
    }

    // --- ProviderLocationUpdatedEvent ---

    [Fact]
    public void Appending_a_location_ping_raises_ProviderLocationUpdatedEvent_carrying_the_fix_itself()
    {
        var providerId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var recordedAtUtc = new DateTime(2026, 8, 7, 9, 30, 0, DateTimeKind.Utc);

        var ping = new ProviderLocationPing(
            Guid.NewGuid(), providerId, bookingId, 12.9716m, 77.5946m, 8.5m, recordedAtUtc, recordedAtUtc.AddSeconds(3));

        var raised = ping.DomainEvents.OfType<ProviderLocationUpdatedEvent>().Should().ContainSingle().Subject;
        raised.PingId.Should().Be(ping.Id);
        raised.ProviderId.Should().Be(providerId);
        raised.BookingId.Should().Be(bookingId);
        raised.Latitude.Should().Be(12.9716m);
        raised.Longitude.Should().Be(77.5946m);
        raised.AccuracyMetres.Should().Be(8.5m);
        // The device's stamp, not the server's - a subscriber deciding whether
        // to draw this fix needs to know how old the fix is.
        raised.RecordedAtUtc.Should().Be(recordedAtUtc);
    }

    /// <summary>
    /// An idle fix still raises, with a null booking. Task 274's broadcast keys
    /// off that null to decide there is no tracking group to push to - it must
    /// not be filtered out down here, where the reason is not known.
    /// </summary>
    [Fact]
    public void An_idle_ping_raises_the_event_with_no_booking()
    {
        var ping = new ProviderLocationPing(
            Guid.NewGuid(), Guid.NewGuid(), null, 12.9716m, 77.5946m, null, DateTime.UtcNow, DateTime.UtcNow);

        ping.DomainEvents.OfType<ProviderLocationUpdatedEvent>().Should().ContainSingle()
            .Which.BookingId.Should().BeNull();
    }

    [Fact]
    public void A_ping_rejected_by_its_own_validation_raises_nothing()
    {
        var outOfRange = () => new ProviderLocationPing(
            Guid.NewGuid(), Guid.NewGuid(), null, 91m, 77.5946m, null, DateTime.UtcNow, DateTime.UtcNow);

        outOfRange.Should().Throw<ArgumentOutOfRangeException>();
    }

    // --- The PII rule these payloads exist under ---

    /// <summary>
    /// Task 274 pushes these payloads straight to browsers over SignalR, so
    /// anything in one is on the wire. Ids, coordinates and timestamps only -
    /// no customer name, no provider phone, no address. A string property is
    /// the shape every one of those would take, so the absence of strings is
    /// the cheap structural guard; a reviewer adding a display name to any of
    /// these fails here rather than in a privacy review after launch.
    /// </summary>
    [Theory]
    [InlineData(typeof(ProviderAssignmentAcceptedEvent))]
    [InlineData(typeof(ProviderEnRouteEvent))]
    [InlineData(typeof(ProviderArrivedEvent))]
    [InlineData(typeof(ProviderLocationUpdatedEvent))]
    [InlineData(typeof(BookingEtaUpdatedEvent))]
    public void No_tracking_event_payload_carries_free_text(Type eventType)
    {
        var textProperties = eventType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(string))
            .Select(p => p.Name);

        textProperties.Should().BeEmpty();
    }

    // --- Dispatch: the aggregate-root promotion is load-bearing ---

    private sealed class TrackingEventRecorder :
        INotificationHandler<DomainEventNotification<ProviderAssignmentAcceptedEvent>>,
        INotificationHandler<DomainEventNotification<ProviderLocationUpdatedEvent>>
    {
        public static readonly List<IDomainEvent> Received = [];

        public Task Handle(DomainEventNotification<ProviderAssignmentAcceptedEvent> notification, CancellationToken cancellationToken)
        {
            lock (Received)
            {
                Received.Add(notification.DomainEvent);
            }

            return Task.CompletedTask;
        }

        public Task Handle(DomainEventNotification<ProviderLocationUpdatedEvent> notification, CancellationToken cancellationToken)
        {
            lock (Received)
            {
                Received.Add(notification.DomainEvent);
            }

            return Task.CompletedTask;
        }
    }

    private static IReadOnlyList<IDomainEvent> ReceivedSnapshot()
    {
        lock (TrackingEventRecorder.Received)
        {
            return TrackingEventRecorder.Received.ToList();
        }
    }

    /// <summary>
    /// A context wired exactly like <see cref="DomainEventDispatchTests"/>':
    /// real entity configurations via EnsureCreated on in-memory SQLite, with
    /// the real interceptor attached.
    /// </summary>
    /// <remarks>
    /// <c>EnsureCreated</c>, so the schema comes from the entity
    /// configurations and never from a migration - the same divergence from
    /// the PostgreSQL runtime that <c>ProviderLocationPingRepositoryTests</c>
    /// records. EF Core's SQLite provider enables <c>PRAGMA foreign_keys</c>
    /// on its own, so <c>booking_provider_assignment</c>'s foreign keys are
    /// live here and the customer/booking/provider rows below have to be real.
    /// </remarks>
    private static async Task WithDispatchingContextAsync(Func<NestlyDbContext, Task> body)
    {
        var services = new ServiceCollection();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(BookingTrackingEventsTests).Assembly));
        services.AddSingleton<DomainEventDispatchInterceptor>();
        await using var serviceProvider = services.BuildServiceProvider();

        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<NestlyDbContext>()
            .UseSqlite(connection)
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(serviceProvider.GetRequiredService<DomainEventDispatchInterceptor>())
            .Options;

        using var context = new NestlyDbContext(options);
        await context.Database.EnsureCreatedAsync();

        await body(context);
    }

    [Fact]
    public async Task Saving_an_accepted_assignment_publishes_ProviderAssignmentAcceptedEvent_and_clears_it()
    {
        await WithDispatchingContextAsync(async context =>
        {
            var customer = new Customer(Guid.NewGuid(), "9" + Guid.NewGuid().ToString("N")[..9], "Asha Rao", CustomerStatus.Active, "asha@example.com");
            var booking = new Booking(Guid.NewGuid(), customer.Id, new CustomerSnapshot(customer.Name, customer.Mobile), null, Address, Slot, Price);
            var provider = new Provider(Guid.NewGuid(), "Ravi Kumar", "Ravi's Repairs", ProviderType.Individual, "+919876543210");
            context.AddRange(customer, booking, provider);
            await context.SaveChangesAsync();

            var assignment = NewAssignment(booking.Id, provider.Id);
            context.Add(assignment);
            await context.SaveChangesAsync();

            // Accept on the already-persisted row, the way
            // BookingProviderAssignmentService.AcceptAsync does it - so this
            // also pins that the interceptor picks up a *modified* aggregate,
            // not only a newly added one.
            assignment.Accept();
            await context.SaveChangesAsync();

            ReceivedSnapshot().OfType<ProviderAssignmentAcceptedEvent>()
                .Should().ContainSingle(e => e.AssignmentId == assignment.Id);
            assignment.DomainEvents.Should().BeEmpty();
        });
    }

    [Fact]
    public async Task Saving_a_location_ping_publishes_ProviderLocationUpdatedEvent_and_clears_it()
    {
        await WithDispatchingContextAsync(async context =>
        {
            var ping = new ProviderLocationPing(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 12.9716m, 77.5946m, 8.5m, DateTime.UtcNow, DateTime.UtcNow);
            context.Add(ping);

            await context.SaveChangesAsync();

            ReceivedSnapshot().OfType<ProviderLocationUpdatedEvent>()
                .Should().ContainSingle(e => e.PingId == ping.Id);
            ping.DomainEvents.Should().BeEmpty();
        });
    }
}

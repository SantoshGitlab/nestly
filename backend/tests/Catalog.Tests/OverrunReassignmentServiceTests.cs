using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Nestly.Application;
using Nestly.Application.ProviderManagement;
using Nestly.Application.Routing;
using Nestly.Domain;
using Nestly.Infrastructure.Options;
using Nestly.Infrastructure.Persistence;
using Nestly.Infrastructure.Persistence.Repositories;
using Nestly.Infrastructure.Services;

namespace Nestly.Catalog.Tests;

/// <summary>
/// Covers the provider-queue model's overrun handling
/// (<see cref="IOverrunReassignmentService"/>): a job that finished late has
/// pushed the provider's other same-day queued (accepted, not yet started)
/// jobs' available travel gap below what the trip actually needs, so those
/// are withdrawn and returned to the assignable pool rather than silently
/// guaranteeing a late arrival.
/// </summary>
public sealed class OverrunReassignmentServiceTests
{
    private static readonly DateOnly SlotDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10));
    private static readonly GeoCoordinate JobAAddress = new(12.9352m, 77.6245m);
    private static readonly GeoCoordinate JobBAddress = new(12.9716m, 77.5946m);

    /// <summary>Returns a fixed duration for one destination, zero for anything else - the minimum a test needs to pin one leg.</summary>
    private sealed class FixedDurationRouteEstimateProvider(GeoCoordinate destination, int durationSeconds) : IRouteEstimateProvider
    {
        public Task<IReadOnlyList<RouteEstimate>> EstimateAsync(
            GeoCoordinate origin, IReadOnlyList<GeoCoordinate> destinations, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RouteEstimate>>(destinations
                .Select((d, i) => new RouteEstimate(
                    i,
                    DistanceMetres: d.Equals(destination) ? durationSeconds * 8 : 0,
                    DurationSeconds: d.Equals(destination) ? durationSeconds : 0,
                    RouteEstimateSource.GoogleMaps))
                .ToList());
    }

    private sealed record Fixture(Guid CustomerId, Guid ProviderId);

    private static async Task<Fixture> SeedAsync(NestlyDbContext context)
    {
        var customer = new Customer(Guid.NewGuid(), "9" + Guid.NewGuid().ToString("N")[..9], "Asha Rao", CustomerStatus.Active);
        var provider = new Provider(Guid.NewGuid(), "Ravi Kumar", "Ravi's Repairs", ProviderType.Individual, "+9198" + Guid.NewGuid().ToString("N")[..8]);
        context.Add(customer);
        context.Add(provider);
        await context.SaveChangesAsync();
        return new Fixture(customer.Id, provider.Id);
    }

    private static Booking NewBooking(Guid customerId, TimeSpan start, TimeSpan end, GeoCoordinate address) =>
        new(
            Guid.NewGuid(), customerId, new CustomerSnapshot("Asha Rao", "9876543210"), null,
            new AddressSnapshot("Home", "221B Baker Street", null, null, "560034", "Bengaluru", "Karnataka",
                address.Latitude, address.Longitude, "Asha Rao", "9876543210"),
            new SlotSnapshot(Guid.NewGuid(), SlotDate, "Window", start, end),
            new PriceSnapshot(500m, 1, 500m, 0m, 50m, 550m, 18m, 99m, 10m, 659m));

    private static OverrunReassignmentService BuildService(NestlyDbContext context, IRouteEstimateProvider router) => new(
        context,
        new BookingRepository(context),
        new BookingProviderAssignmentRepository(context),
        TravelFeasibilityFactory.Build(
            context, router, new SandboxRouteEstimateProvider(Microsoft.Extensions.Options.Options.Create(new SandboxRouteEstimateOptions())),
            new AutoAssignmentOptions { TravelHandoverBufferMinutes = 20 }),
        NullLogger<OverrunReassignmentService>.Instance);

    [Fact]
    public async Task An_overrun_that_leaves_too_little_travel_time_withdraws_the_queued_job()
    {
        using var db = new TestDatabase();
        await using var context = db.CreateContext();
        var f = await SeedAsync(context);

        // Job A: booked 9:00-11:00, actually finished (overran) at 11:30 -
        // needs a Completed assignment of its own, or the travel-feasibility
        // check has no "previous leg" to find at all.
        var jobA = NewBooking(f.CustomerId, TimeSpan.FromHours(9), TimeSpan.FromHours(11), JobAAddress);
        context.Add(jobA);
        var jobAAssignment = new BookingProviderAssignment(Guid.NewGuid(), jobA.Id, f.ProviderId, BookingAssignedByType.System, null, null);
        jobAAssignment.Accept();
        jobAAssignment.Complete(SlotDate.ToDateTime(TimeOnly.FromTimeSpan(TimeSpan.FromHours(11.5)), DateTimeKind.Utc));
        context.Add(jobAAssignment);

        // Job B: queued (accepted, not started), 12:00-13:00 - only 30 minutes
        // away from job A's actual 11:30 finish.
        var jobB = NewBooking(f.CustomerId, TimeSpan.FromHours(12), TimeSpan.FromHours(13), JobBAddress);
        jobB.TransitionTo(BookingStatus.PaymentPending);
        jobB.TransitionTo(BookingStatus.Confirmed);
        jobB.TransitionTo(BookingStatus.AwaitingFulfilment);
        jobB.TransitionTo(BookingStatus.Assigned);
        jobB.AssignProvider(f.ProviderId);
        context.Add(jobB);
        var jobBAssignment = new BookingProviderAssignment(Guid.NewGuid(), jobB.Id, f.ProviderId, BookingAssignedByType.System, null, null);
        jobBAssignment.Accept();
        context.Add(jobBAssignment);
        await context.SaveChangesAsync();

        // 40 minutes' drive plus the 20-minute buffer (60 min required) against
        // only 30 minutes of gap - infeasible.
        var router = new FixedDurationRouteEstimateProvider(JobAAddress, durationSeconds: 40 * 60);

        await BuildService(context, router).ReassignInfeasibleQueuedJobsAsync(f.ProviderId, SlotDate, jobA.Id);

        await using var verifyContext = db.CreateContext();
        var reloadedJobB = await new BookingRepository(verifyContext).GetByIdAsync(jobB.Id);
        reloadedJobB!.Status.Should().Be(BookingStatus.AwaitingFulfilment);
        reloadedJobB.AssignedProviderId.Should().BeNull();

        var reloadedAssignment = await new BookingProviderAssignmentRepository(verifyContext).GetByIdAsync(jobBAssignment.Id);
        reloadedAssignment!.Status.Should().Be(BookingProviderAssignmentStatus.Withdrawn);
    }

    [Fact]
    public async Task An_overrun_that_still_leaves_enough_travel_time_leaves_the_queued_job_untouched()
    {
        using var db = new TestDatabase();
        await using var context = db.CreateContext();
        var f = await SeedAsync(context);

        var jobA = NewBooking(f.CustomerId, TimeSpan.FromHours(9), TimeSpan.FromHours(11), JobAAddress);
        context.Add(jobA);
        var jobAAssignment = new BookingProviderAssignment(Guid.NewGuid(), jobA.Id, f.ProviderId, BookingAssignedByType.System, null, null);
        jobAAssignment.Accept();
        jobAAssignment.Complete(SlotDate.ToDateTime(TimeOnly.FromTimeSpan(TimeSpan.FromHours(11.5)), DateTimeKind.Utc));
        context.Add(jobAAssignment);

        // Job B starts much later - 14:00-15:00 - so the same overrun still
        // leaves a comfortable 2.5-hour gap.
        var jobB = NewBooking(f.CustomerId, TimeSpan.FromHours(14), TimeSpan.FromHours(15), JobBAddress);
        jobB.TransitionTo(BookingStatus.PaymentPending);
        jobB.TransitionTo(BookingStatus.Confirmed);
        jobB.TransitionTo(BookingStatus.AwaitingFulfilment);
        jobB.TransitionTo(BookingStatus.Assigned);
        jobB.AssignProvider(f.ProviderId);
        context.Add(jobB);
        var jobBAssignment = new BookingProviderAssignment(Guid.NewGuid(), jobB.Id, f.ProviderId, BookingAssignedByType.System, null, null);
        jobBAssignment.Accept();
        context.Add(jobBAssignment);
        await context.SaveChangesAsync();

        var router = new FixedDurationRouteEstimateProvider(JobAAddress, durationSeconds: 40 * 60);

        await BuildService(context, router).ReassignInfeasibleQueuedJobsAsync(f.ProviderId, SlotDate, jobA.Id);

        await using var verifyContext = db.CreateContext();
        var reloadedJobB = await new BookingRepository(verifyContext).GetByIdAsync(jobB.Id);
        reloadedJobB!.Status.Should().Be(BookingStatus.Assigned, "the 2.5-hour gap comfortably covers a 40-minute drive plus buffer");
        reloadedJobB.AssignedProviderId.Should().Be(f.ProviderId);

        var reloadedAssignment = await new BookingProviderAssignmentRepository(verifyContext).GetByIdAsync(jobBAssignment.Id);
        reloadedAssignment!.Status.Should().Be(BookingProviderAssignmentStatus.Accepted);
    }
}

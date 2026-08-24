using FluentAssertions;
using Nestly.Application;
using Nestly.Domain;
using Nestly.Infrastructure.Services;

namespace Nestly.Catalog.Tests;

/// <summary>
/// Stage 2 of the provider-queue early-release model: proves the effective-
/// end-time wiring (<see cref="ProviderJobOccupancyServiceTests"/>) actually
/// changes what <see cref="ProviderScheduleConflictService"/> decides -
/// releasing a provider early for a non-duration-based job that finished
/// ahead of schedule, keeping a duration-based one committed regardless, and
/// correctly extending the block when a job overran. Companion to
/// <see cref="ProviderQueueEarlyReleaseFoundationTests"/>, which pins Stage
/// 1's behaviour-neutral baseline this stage now changes on purpose.
/// </summary>
public sealed class ProviderQueueEarlyReleaseBehaviorTests
{
    private static readonly DateOnly SlotDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10));

    private sealed record Fixture(Guid CustomerId, Guid ProviderId);

    private static async Task<Fixture> SeedAsync(Nestly.Infrastructure.Persistence.NestlyDbContext context)
    {
        var customer = new Customer(Guid.NewGuid(), "9" + Guid.NewGuid().ToString("N")[..9], "Asha Rao", CustomerStatus.Active);
        var provider = new Provider(Guid.NewGuid(), "Ravi Kumar", "Ravi's Repairs", ProviderType.Individual, "+9198" + Guid.NewGuid().ToString("N")[..8]);
        context.Add(customer);
        context.Add(provider);
        await context.SaveChangesAsync();
        return new Fixture(customer.Id, provider.Id);
    }

    private static Booking NewBooking(Guid customerId, TimeSpan start, TimeSpan end) =>
        new(
            Guid.NewGuid(), customerId, new CustomerSnapshot("Asha Rao", "9876543210"), null,
            new AddressSnapshot("Home", "221B Baker Street", null, null, "560034", "Bengaluru", "Karnataka",
                12.9352m, 77.6245m, "Asha Rao", "9876543210"),
            new SlotSnapshot(Guid.NewGuid(), SlotDate, "Window", start, end),
            new PriceSnapshot(500m, 1, 500m, 0m, 50m, 550m, 18m, 99m, 10m, 659m));

    private static BookingProviderAssignment NewCompletedAssignment(
        Guid bookingId, Guid providerId, DateTime completedAtUtc)
    {
        var assignment = new BookingProviderAssignment(
            Guid.NewGuid(), bookingId, providerId, BookingAssignedByType.System, null, null);
        assignment.Accept();
        assignment.Complete(completedAtUtc);
        return assignment;
    }

    [Fact]
    public async Task A_non_duration_based_job_that_finished_early_no_longer_blocks_a_slot_it_used_to_overlap()
    {
        using var db = new TestDatabase();
        await using var context = db.CreateContext();
        var f = await SeedAsync(context);

        // Booked 9:00-11:00, marked Done at 10:15 (45 minutes early).
        var existing = NewBooking(f.CustomerId, TimeSpan.FromHours(9), TimeSpan.FromHours(11));
        context.Add(existing);
        var completedAtUtc = SlotDate.ToDateTime(TimeOnly.FromTimeSpan(TimeSpan.FromHours(10.25)), DateTimeKind.Utc);
        context.Add(NewCompletedAssignment(existing.Id, f.ProviderId, completedAtUtc));

        // A candidate that overlaps the ORIGINAL 9-11 slot (10:30-12:00) but not
        // the actual 9:00-10:15 occupied window.
        var candidate = NewBooking(f.CustomerId, TimeSpan.FromHours(10.5), TimeSpan.FromHours(12));
        context.Add(candidate);
        await context.SaveChangesAsync();

        var conflict = await new ProviderScheduleConflictService(context, TestServices.Occupancy())
            .FindConflictAsync(f.ProviderId, candidate);

        conflict.Should().BeNull("the provider genuinely finished at 10:15 and is free from then on");
    }

    [Fact]
    public async Task A_duration_based_job_still_blocks_an_overlapping_slot_even_after_it_is_marked_complete()
    {
        using var db = new TestDatabase();
        await using var context = db.CreateContext();
        var f = await SeedAsync(context);

        // Same scenario as the release test above, but the booked service is
        // duration-based - the customer bought the 9-11 block, so completing
        // early must not free the provider for it.
        var durationBasedExisting = NewDurationBasedBooking(f.CustomerId, TimeSpan.FromHours(9), TimeSpan.FromHours(11));
        context.Add(durationBasedExisting);
        var completedAtUtc = SlotDate.ToDateTime(TimeOnly.FromTimeSpan(TimeSpan.FromHours(10.25)), DateTimeKind.Utc);
        context.Add(NewCompletedAssignment(durationBasedExisting.Id, f.ProviderId, completedAtUtc));

        var candidate = NewBooking(f.CustomerId, TimeSpan.FromHours(10.5), TimeSpan.FromHours(12));
        context.Add(candidate);
        await context.SaveChangesAsync();

        var conflict = await new ProviderScheduleConflictService(context, TestServices.Occupancy())
            .FindConflictAsync(f.ProviderId, candidate);

        conflict.Should().NotBeNull("the customer bought a block of time - finishing the checklist early does not release it");
        conflict!.BookingId.Should().Be(durationBasedExisting.Id);
    }

    [Fact]
    public async Task A_job_that_overran_its_slot_blocks_a_slot_that_would_otherwise_have_been_legal()
    {
        using var db = new TestDatabase();
        await using var context = db.CreateContext();
        var f = await SeedAsync(context);

        // Booked 9:00-11:00, but actually finished at 11:20 (a 20-minute overrun).
        var existing = NewBooking(f.CustomerId, TimeSpan.FromHours(9), TimeSpan.FromHours(11));
        context.Add(existing);
        var completedAtUtc = SlotDate.ToDateTime(TimeOnly.FromTimeSpan(TimeSpan.FromHours(11) + TimeSpan.FromMinutes(20)), DateTimeKind.Utc);
        context.Add(NewCompletedAssignment(existing.Id, f.ProviderId, completedAtUtc));

        // Starts exactly at the ORIGINAL slot end (11:00-12:30) - legal against
        // the booked slot, but the provider was genuinely still on the prior job.
        var candidate = NewBooking(f.CustomerId, TimeSpan.FromHours(11), TimeSpan.FromHours(12.5));
        context.Add(candidate);
        await context.SaveChangesAsync();

        var conflict = await new ProviderScheduleConflictService(context, TestServices.Occupancy())
            .FindConflictAsync(f.ProviderId, candidate);

        conflict.Should().NotBeNull("the provider was still on the overran job until 11:20, not free at the booked 11:00");
        conflict!.BookingId.Should().Be(existing.Id);
    }

    /// <summary>Same shape as <see cref="NewBooking"/>, but for the service the customer bought as a block of time.</summary>
    private static Booking NewDurationBasedBooking(Guid customerId, TimeSpan start, TimeSpan end) =>
        new(
            Guid.NewGuid(), customerId, new CustomerSnapshot("Asha Rao", "9876543210"), null,
            new AddressSnapshot("Home", "221B Baker Street", null, null, "560034", "Bengaluru", "Karnataka",
                12.9352m, 77.6245m, "Asha Rao", "9876543210"),
            new SlotSnapshot(Guid.NewGuid(), SlotDate, "Window", start, end),
            new PriceSnapshot(500m, 1, 500m, 0m, 50m, 550m, 18m, 99m, 10m, 659m),
            couponCode: null, couponDiscountAmount: null, subscriptionId: null, subscriptionFreeVisitApplied: false,
            subscriptionDiscountAmount: null, idempotencyKey: null, recurringBookingPlanId: null,
            walletCreditApplied: null, amcContractId: null,
            serviceDurationMinutes: (int)(end - start).TotalMinutes, isDurationBased: true);
}

using FluentAssertions;
using Nestly.Application;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence.Repositories;
using Nestly.Infrastructure.Services;

namespace Nestly.Catalog.Tests;

/// <summary>
/// Covers the data-model foundation for the provider-queue early-release model
/// (verified completion -> eligible for the next order, subject to
/// availability/travel/buffer/duration/scheduling): the assignment's new
/// terminal <see cref="BookingProviderAssignmentStatus.Completed"/> state,
/// <see cref="Service.IsDurationBased"/>, and the booking-level commitment
/// snapshot that freezes duration/duration-based at booking creation so a
/// later catalog edit never changes an existing booking's commitment.
///
/// This stage is deliberately behaviour-neutral: a Completed assignment must
/// keep occupying the provider's schedule exactly as it did while it stayed
/// Accepted (the early-release math that lets it stop doing so is a separate,
/// deliberate follow-up) - <see cref="A_completed_assignment_still_blocks_an_overlapping_new_assignment"/>
/// pins that invariant.
/// </summary>
public sealed class ProviderQueueEarlyReleaseFoundationTests
{
    private static readonly DateOnly SlotDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10));

    // ---------------------------------------------------------------------
    // BookingProviderAssignment.Complete()
    // ---------------------------------------------------------------------

    [Fact]
    public void Complete_moves_an_accepted_assignment_to_the_terminal_Completed_state_and_stamps_the_finish_time()
    {
        var assignment = new BookingProviderAssignment(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), BookingAssignedByType.System, null, null);
        assignment.Accept();

        var completedAt = DateTime.UtcNow;
        assignment.Complete(completedAt);

        assignment.Status.Should().Be(BookingProviderAssignmentStatus.Completed);
        assignment.CompletedAt.Should().Be(completedAt);
    }

    [Theory]
    [InlineData(BookingProviderAssignmentStatus.Assigned)]
    [InlineData(BookingProviderAssignmentStatus.Rejected)]
    [InlineData(BookingProviderAssignmentStatus.Withdrawn)]
    public void Complete_throws_when_the_assignment_was_never_accepted_or_is_already_terminal(BookingProviderAssignmentStatus status)
    {
        var assignment = new BookingProviderAssignment(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), BookingAssignedByType.System, null, null);

        switch (status)
        {
            case BookingProviderAssignmentStatus.Rejected:
                assignment.Reject(null);
                break;
            case BookingProviderAssignmentStatus.Withdrawn:
                assignment.Withdraw();
                break;
        }

        var act = () => assignment.Complete(DateTime.UtcNow);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Complete_cannot_be_called_twice()
    {
        var assignment = new BookingProviderAssignment(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), BookingAssignedByType.System, null, null);
        assignment.Accept();
        assignment.Complete(DateTime.UtcNow);

        var act = () => assignment.Complete(DateTime.UtcNow);
        act.Should().Throw<InvalidOperationException>();
    }

    // ---------------------------------------------------------------------
    // Service.IsDurationBased
    // ---------------------------------------------------------------------

    [Fact]
    public void A_new_service_defaults_to_not_duration_based()
    {
        var service = new Service(Guid.NewGuid(), Guid.NewGuid(), "AC Repair", "ac-repair-" + Guid.NewGuid(), "desc", 500m);

        service.IsDurationBased.Should().BeFalse();
    }

    [Fact]
    public void SetOptions_can_mark_a_service_duration_based()
    {
        var service = new Service(Guid.NewGuid(), Guid.NewGuid(), "Hourly Cleaning", "hourly-cleaning-" + Guid.NewGuid(), "desc", 500m);

        service.SetOptions(
            isTaxApplicable: true, isAddOnAllowed: true, isQuantityAllowed: false, isInspectionBased: false,
            isSlotRequired: true, isAddressRequired: true, isCustomerNoteAllowed: true, isDurationBased: true);

        service.IsDurationBased.Should().BeTrue();
    }

    // ---------------------------------------------------------------------
    // Booking commitment snapshot
    // ---------------------------------------------------------------------

    [Fact]
    public void A_booking_snapshots_the_services_duration_commitment_at_creation()
    {
        var booking = BuildBooking(serviceDurationMinutes: 120, isDurationBased: true);

        booking.ServiceDurationMinutesSnapshot.Should().Be(120);
        booking.IsDurationBasedSnapshot.Should().BeTrue();
    }

    [Fact]
    public void A_booking_defaults_to_a_zero_non_duration_based_commitment_when_none_is_supplied()
    {
        var booking = BuildBooking();

        booking.ServiceDurationMinutesSnapshot.Should().Be(0);
        booking.IsDurationBasedSnapshot.Should().BeFalse();
    }

    private static Booking BuildBooking(int serviceDurationMinutes = 0, bool isDurationBased = false, Guid? customerId = null)
    {
        var address = new AddressSnapshot(
            "Home", "221B Baker Street", null, null, "560034", "Bengaluru", "Karnataka",
            12.9352m, 77.6245m, "Asha Rao", "9876543210");
        var slot = new SlotSnapshot(Guid.NewGuid(), SlotDate, "Morning", TimeSpan.FromHours(9), TimeSpan.FromHours(11));
        var price = new PriceSnapshot(500m, 1, 500m, 0m, 50m, 550m, 18m, 99m, 10m, 659m);

        return new Booking(
            Guid.NewGuid(), customerId ?? Guid.NewGuid(), new CustomerSnapshot("Asha Rao", "9876543210"), null, address, slot, price,
            couponCode: null, couponDiscountAmount: null, subscriptionId: null, subscriptionFreeVisitApplied: false,
            subscriptionDiscountAmount: null, idempotencyKey: null, recurringBookingPlanId: null,
            walletCreditApplied: null, amcContractId: null,
            serviceDurationMinutes: serviceDurationMinutes, isDurationBased: isDurationBased);
    }

    // ---------------------------------------------------------------------
    // Stage-1 behaviour-neutrality: Completed still occupies the schedule
    // ---------------------------------------------------------------------

    [Fact]
    public async Task A_completed_assignment_still_blocks_an_overlapping_new_assignment()
    {
        using var db = new TestDatabase();
        await using var context = db.CreateContext();

        var customer = new Customer(Guid.NewGuid(), "9" + Guid.NewGuid().ToString("N")[..9], "Asha Rao", CustomerStatus.Active);
        context.Add(customer);
        var customerId = customer.Id;

        var provider = new Provider(Guid.NewGuid(), "Ravi Kumar", "Ravi's Repairs", ProviderType.Individual, "+9198" + Guid.NewGuid().ToString("N")[..8]);
        context.Add(provider);
        var providerId = provider.Id;

        var address = new AddressSnapshot(
            "Home", "221B Baker Street", null, null, "560034", "Bengaluru", "Karnataka",
            12.9352m, 77.6245m, "Asha Rao", "9876543210");
        var price = new PriceSnapshot(500m, 1, 500m, 0m, 50m, 550m, 18m, 99m, 10m, 659m);

        var existingSlot = new SlotSnapshot(Guid.NewGuid(), SlotDate, "Morning", TimeSpan.FromHours(9), TimeSpan.FromHours(11));
        var existingBooking = new Booking(Guid.NewGuid(), customerId, new CustomerSnapshot("Asha Rao", "9876543210"), null, address, existingSlot, price);
        context.Add(existingBooking);

        var existingAssignment = new BookingProviderAssignment(
            Guid.NewGuid(), existingBooking.Id, providerId, BookingAssignedByType.System, null, null);
        existingAssignment.Accept();
        existingAssignment.Complete(DateTime.UtcNow);
        context.Add(existingAssignment);

        // Overlaps the existing 09:00-11:00 job (candidate 10:00-12:00).
        var candidateSlot = new SlotSnapshot(Guid.NewGuid(), SlotDate, "Late morning", TimeSpan.FromHours(10), TimeSpan.FromHours(12));
        var candidateBooking = new Booking(Guid.NewGuid(), customerId, new CustomerSnapshot("Asha Rao", "9876543210"), null, address, candidateSlot, price);
        context.Add(candidateBooking);
        await context.SaveChangesAsync();

        var conflictService = new ProviderScheduleConflictService(context, TestServices.Occupancy());
        var conflict = await conflictService.FindConflictAsync(providerId, candidateBooking);

        // Stage 1 is deliberately behaviour-neutral: a Completed job still
        // occupies its full slot window, exactly as an Accepted one would -
        // shrinking that window down to the actual finish time for a
        // non-duration-based service is the early-release follow-up.
        conflict.Should().NotBeNull();
        conflict!.BookingId.Should().Be(existingBooking.Id);
    }

    // ---------------------------------------------------------------------
    // IBookingProviderAssignmentRepository.GetCurrentByBookingAsync
    // ---------------------------------------------------------------------

    [Fact]
    public async Task GetCurrentByBookingAsync_finds_a_completed_assignment_that_GetActiveByBookingAsync_does_not()
    {
        using var db = new TestDatabase();
        await using var context = db.CreateContext();

        var customer = new Customer(Guid.NewGuid(), "9" + Guid.NewGuid().ToString("N")[..9], "Asha Rao", CustomerStatus.Active);
        var provider = new Provider(Guid.NewGuid(), "Ravi Kumar", "Ravi's Repairs", ProviderType.Individual, "+9198" + Guid.NewGuid().ToString("N")[..8]);
        context.Add(customer);
        context.Add(provider);

        var booking = BuildBooking(customerId: customer.Id);
        context.Add(booking);

        var assignment = new BookingProviderAssignment(
            Guid.NewGuid(), booking.Id, provider.Id, BookingAssignedByType.System, null, null);
        assignment.Accept();
        assignment.Complete(DateTime.UtcNow);
        context.Add(assignment);
        await context.SaveChangesAsync();

        var repository = new BookingProviderAssignmentRepository(context);

        (await repository.GetActiveByBookingAsync(booking.Id)).Should().BeNull(
            "a Completed assignment is no longer 'outstanding' - write paths (cancel/reschedule) must not touch it");
        (await repository.GetCurrentByBookingAsync(booking.Id)).Should().NotBeNull(
            "read paths (a customer's/provider's own view of the job) must still find who did it");
    }

    [Theory]
    [InlineData(BookingProviderAssignmentStatus.Rejected)]
    [InlineData(BookingProviderAssignmentStatus.Withdrawn)]
    public async Task GetCurrentByBookingAsync_excludes_a_superseded_assignment(BookingProviderAssignmentStatus status)
    {
        using var db = new TestDatabase();
        await using var context = db.CreateContext();

        var customer = new Customer(Guid.NewGuid(), "9" + Guid.NewGuid().ToString("N")[..9], "Asha Rao", CustomerStatus.Active);
        var provider = new Provider(Guid.NewGuid(), "Ravi Kumar", "Ravi's Repairs", ProviderType.Individual, "+9198" + Guid.NewGuid().ToString("N")[..8]);
        context.Add(customer);
        context.Add(provider);

        var booking = BuildBooking(customerId: customer.Id);
        context.Add(booking);

        var assignment = new BookingProviderAssignment(
            Guid.NewGuid(), booking.Id, provider.Id, BookingAssignedByType.System, null, null);
        if (status == BookingProviderAssignmentStatus.Withdrawn)
        {
            assignment.Withdraw();
        }
        else
        {
            assignment.Reject(null);
        }
        context.Add(assignment);
        await context.SaveChangesAsync();

        var repository = new BookingProviderAssignmentRepository(context);

        (await repository.GetCurrentByBookingAsync(booking.Id)).Should().BeNull();
    }
}

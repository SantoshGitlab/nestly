using FluentAssertions;
using Nestly.Application.ProviderManagement;
using Nestly.Domain;
using Nestly.Infrastructure.Services;

namespace Nestly.Catalog.Tests;

/// <summary>
/// Pure unit coverage of <see cref="ProviderJobOccupancyService"/> - the
/// provider-queue early-release model's core computation: when a job stops
/// occupying a provider's schedule. See the interface doc comment for the
/// full rule; these pin each branch independently of the services that
/// consume it (<c>ProviderScheduleConflictServiceTests</c> et al. cover the
/// end-to-end effect on overlap/travel).
/// </summary>
public sealed class ProviderJobOccupancyServiceTests
{
    private static readonly DateOnly SlotDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10));
    private static readonly TimeSpan SlotStart = TimeSpan.FromHours(9);
    private static readonly TimeSpan SlotEnd = TimeSpan.FromHours(11);

    // UTC-pinned clock so a UTC-based CompletedAt and the UTC-based SlotDate
    // this test builds sit on the same calendar day without a timezone offset
    // to account for - the offset behaviour itself is BusinessClockTests' job.
    private static readonly ProviderJobOccupancyService Occupancy = new(TestServices.Clock());

    private static JobOccupancy Job(
        BookingProviderAssignmentStatus status, DateTime? completedAtUtc, bool isDurationBased = false) =>
        new(status, completedAtUtc, isDurationBased, SlotDate, SlotStart, SlotEnd);

    [Fact]
    public void An_accepted_not_yet_completed_job_occupies_through_its_slot_end()
    {
        Occupancy.EffectiveEndTime(Job(BookingProviderAssignmentStatus.Accepted, null))
            .Should().Be(SlotEnd);
    }

    [Fact]
    public void A_completed_duration_based_job_occupies_through_its_slot_end_regardless_of_when_it_actually_finished()
    {
        // Finished 90 minutes early - a duration-based service stays
        // committed for the full booked window either way.
        var completedAtUtc = SlotDate.ToDateTime(TimeOnly.FromTimeSpan(SlotEnd.Subtract(TimeSpan.FromMinutes(90))), DateTimeKind.Utc);

        Occupancy.EffectiveEndTime(Job(BookingProviderAssignmentStatus.Completed, completedAtUtc, isDurationBased: true))
            .Should().Be(SlotEnd);
    }

    [Fact]
    public void A_completed_non_duration_based_job_that_finished_early_releases_at_the_actual_finish_time()
    {
        var actualFinish = SlotEnd.Subtract(TimeSpan.FromMinutes(45));
        var completedAtUtc = SlotDate.ToDateTime(TimeOnly.FromTimeSpan(actualFinish), DateTimeKind.Utc);

        Occupancy.EffectiveEndTime(Job(BookingProviderAssignmentStatus.Completed, completedAtUtc))
            .Should().Be(actualFinish);
    }

    [Fact]
    public void A_completed_non_duration_based_job_that_overran_extends_occupancy_past_the_slots_own_end()
    {
        var actualFinish = SlotEnd.Add(TimeSpan.FromMinutes(30));
        var completedAtUtc = SlotDate.ToDateTime(TimeOnly.FromTimeSpan(actualFinish), DateTimeKind.Utc);

        Occupancy.EffectiveEndTime(Job(BookingProviderAssignmentStatus.Completed, completedAtUtc))
            .Should().Be(actualFinish);
    }

    [Fact]
    public void A_completion_time_before_the_slots_own_start_is_clamped_to_the_start_not_treated_as_an_early_release()
    {
        // A data/clock anomaly - "finished" before the job began - must never
        // widen the provider's apparent availability.
        var beforeStart = SlotStart.Subtract(TimeSpan.FromMinutes(10));
        var completedAtUtc = SlotDate.ToDateTime(TimeOnly.FromTimeSpan(beforeStart), DateTimeKind.Utc);

        Occupancy.EffectiveEndTime(Job(BookingProviderAssignmentStatus.Completed, completedAtUtc))
            .Should().Be(SlotStart);
    }

    [Fact]
    public void An_overrun_past_midnight_occupies_the_rest_of_the_slot_date()
    {
        var completedAtUtc = SlotDate.AddDays(1).ToDateTime(TimeOnly.FromTimeSpan(TimeSpan.FromHours(1)), DateTimeKind.Utc);

        Occupancy.EffectiveEndTime(Job(BookingProviderAssignmentStatus.Completed, completedAtUtc))
            .Should().Be(TimeSpan.FromHours(24));
    }

    [Fact]
    public void A_completion_time_before_the_slot_date_is_a_clock_anomaly_that_never_shrinks_occupancy()
    {
        var completedAtUtc = SlotDate.AddDays(-1).ToDateTime(TimeOnly.FromTimeSpan(TimeSpan.FromHours(20)), DateTimeKind.Utc);

        Occupancy.EffectiveEndTime(Job(BookingProviderAssignmentStatus.Completed, completedAtUtc))
            .Should().Be(SlotEnd);
    }

    [Fact]
    public void A_completed_assignment_with_no_recorded_completion_time_falls_back_to_the_slots_own_end()
    {
        Occupancy.EffectiveEndTime(Job(BookingProviderAssignmentStatus.Completed, completedAtUtc: null))
            .Should().Be(SlotEnd);
    }
}

using FluentAssertions;
using Nestly.Domain;

namespace Nestly.Catalog.Tests;

/// <summary>
/// Pure domain-logic coverage for <see cref="RecurringBookingPlan"/> (tasks
/// 184-186): occurrence-date generation from a recurrence rule, the
/// skipped-occurrence-doesn't-count-against-budget open decision, and
/// pause/resume/cancel transitions. No database needed - the aggregate holds
/// no infrastructure dependencies.
/// </summary>
public sealed class RecurringBookingPlanTests
{
    private static RecurringBookingPlan WeeklyPlan(DateOnly startDate, DayOfWeek dayOfWeek, int? occurrenceCount = 4) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            quantity: 1, RecurringBookingRecurrenceFrequency.Weekly, dayOfWeek, recurrenceDayOfMonth: null,
            startDate, endDate: null, occurrenceCount);

    private static RecurringBookingPlan MonthlyPlan(DateOnly startDate, int dayOfMonth, int? occurrenceCount = 6) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            quantity: 1, RecurringBookingRecurrenceFrequency.Monthly, recurrenceDayOfWeek: null, dayOfMonth,
            startDate, endDate: null, occurrenceCount);

    [Fact]
    public void Ctor_weekly_plan_rolls_forward_to_the_next_matching_day_of_week()
    {
        // 2026-08-01 is a Saturday; asking for Tuesday should land on 2026-08-04.
        var start = new DateOnly(2026, 8, 1);
        var plan = WeeklyPlan(start, DayOfWeek.Tuesday);

        plan.NextOccurrenceDate.Should().Be(new DateOnly(2026, 8, 4));
    }

    [Fact]
    public void Ctor_weekly_plan_keeps_start_date_when_it_already_matches_the_day_of_week()
    {
        var start = new DateOnly(2026, 8, 1); // Saturday
        var plan = WeeklyPlan(start, DayOfWeek.Saturday);

        plan.NextOccurrenceDate.Should().Be(start);
    }

    [Fact]
    public void Ctor_monthly_plan_clamps_day_of_month_to_the_actual_month_length()
    {
        // Requesting day 31 in a 30-day-or-fewer month must clamp, not throw or roll into the next month.
        var start = new DateOnly(2026, 2, 1);
        var plan = MonthlyPlan(start, dayOfMonth: 31);

        plan.NextOccurrenceDate.Should().Be(new DateOnly(2026, 2, 28));
    }

    [Fact]
    public void RecordOccurrenceBooked_advances_the_next_date_and_increments_completed_count()
    {
        var plan = WeeklyPlan(new DateOnly(2026, 8, 4), DayOfWeek.Tuesday, occurrenceCount: 4);
        var due = plan.NextOccurrenceDate;

        plan.RecordOccurrenceBooked(due);

        plan.CompletedOccurrenceCount.Should().Be(1);
        plan.NextOccurrenceDate.Should().Be(due.AddDays(7));
        plan.Status.Should().Be(RecurringBookingPlanStatus.Active);
    }

    [Fact]
    public void RecordOccurrenceSkipped_advances_the_next_date_but_does_not_count_against_the_occurrence_budget()
    {
        // OPEN DECISION (RecurringBookingPlan doc comment): a supply-side
        // miss must not cost the customer one of their paid-for occurrences.
        var plan = WeeklyPlan(new DateOnly(2026, 8, 4), DayOfWeek.Tuesday, occurrenceCount: 4);
        var due = plan.NextOccurrenceDate;

        plan.RecordOccurrenceSkipped(due);

        plan.CompletedOccurrenceCount.Should().Be(0);
        plan.NextOccurrenceDate.Should().Be(due.AddDays(7));
        plan.Status.Should().Be(RecurringBookingPlanStatus.Active);
    }

    [Fact]
    public void Plan_completes_once_the_occurrence_budget_is_exhausted()
    {
        var plan = WeeklyPlan(new DateOnly(2026, 8, 4), DayOfWeek.Tuesday, occurrenceCount: 2);

        plan.RecordOccurrenceBooked(plan.NextOccurrenceDate);
        plan.Status.Should().Be(RecurringBookingPlanStatus.Active);

        plan.RecordOccurrenceBooked(plan.NextOccurrenceDate);
        plan.Status.Should().Be(RecurringBookingPlanStatus.Completed);
    }

    [Fact]
    public void RecordOccurrence_throws_when_the_date_does_not_match_the_plans_next_due_date()
    {
        var plan = WeeklyPlan(new DateOnly(2026, 8, 4), DayOfWeek.Tuesday);

        var act = () => plan.RecordOccurrenceBooked(plan.NextOccurrenceDate.AddDays(1));

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Pause_then_Resume_preserves_the_next_occurrence_date()
    {
        var plan = WeeklyPlan(new DateOnly(2026, 8, 4), DayOfWeek.Tuesday);
        var dueBeforePause = plan.NextOccurrenceDate;

        plan.Pause();
        plan.Status.Should().Be(RecurringBookingPlanStatus.Paused);

        plan.Resume();
        plan.Status.Should().Be(RecurringBookingPlanStatus.Active);
        plan.NextOccurrenceDate.Should().Be(dueBeforePause);
    }

    [Fact]
    public void Pause_throws_when_the_plan_is_not_active()
    {
        var plan = WeeklyPlan(new DateOnly(2026, 8, 4), DayOfWeek.Tuesday);
        plan.Cancel();

        var act = () => plan.Pause();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Cancel_is_terminal_and_cannot_be_resumed()
    {
        var plan = WeeklyPlan(new DateOnly(2026, 8, 4), DayOfWeek.Tuesday);
        plan.Cancel();

        plan.Status.Should().Be(RecurringBookingPlanStatus.Cancelled);
        var act = () => plan.Resume();
        act.Should().Throw<InvalidOperationException>();

        var cancelAgain = () => plan.Cancel();
        cancelAgain.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Ctor_throws_when_neither_end_date_nor_occurrence_count_is_set()
    {
        var act = () => new RecurringBookingPlan(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            quantity: 1, RecurringBookingRecurrenceFrequency.Weekly, DayOfWeek.Monday, recurrenceDayOfMonth: null,
            startDate: new DateOnly(2026, 8, 1), endDate: null, occurrenceCount: null);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void PreviewUpcomingOccurrenceDates_returns_the_requested_count_capped_by_the_remaining_budget()
    {
        var plan = WeeklyPlan(new DateOnly(2026, 8, 4), DayOfWeek.Tuesday, occurrenceCount: 3);
        plan.RecordOccurrenceBooked(plan.NextOccurrenceDate); // 1 of 3 used up

        var preview = plan.PreviewUpcomingOccurrenceDates(10);

        preview.Should().HaveCount(2); // only 2 occurrences left in the budget
        preview.Should().BeInAscendingOrder();
        preview[0].Should().Be(plan.NextOccurrenceDate);
    }
}

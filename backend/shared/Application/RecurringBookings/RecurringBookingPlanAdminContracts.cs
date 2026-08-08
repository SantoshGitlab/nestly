using Nestly.Domain;

namespace Nestly.Application.RecurringBookings;

/// <summary>
/// Filters for the admin recurring-plan list (task 299). Same shape as
/// <see cref="Coupons.CouponAdminSearchRequest"/>/<c>AdminBookingSearchRequest</c>:
/// every filter optional, paging with the project's standard 1-based
/// page/pageSize pair.
/// </summary>
public sealed record AdminRecurringPlanSearchRequest(
    RecurringBookingPlanStatus? Status,
    RecurringBookingRecurrenceFrequency? Frequency,
    Guid? CustomerId,
    Guid? ServiceId,
    int Page = 1,
    int PageSize = 20);

/// <summary>
/// One row of the admin recurring-plan list. Carries the customer's and
/// service's current names (joined, not snapshotted) because a plan holds
/// live references to both - see <see cref="RecurringBookingPlan"/>'s class
/// doc comment on why nothing about a plan is a snapshot.
/// </summary>
public sealed record AdminRecurringPlanSummaryResponse(
    Guid Id,
    Guid CustomerId,
    string CustomerName,
    Guid ServiceId,
    string ServiceName,
    RecurringBookingRecurrenceFrequency Frequency,
    DayOfWeek? RecurrenceDayOfWeek,
    int? RecurrenceDayOfMonth,
    DateOnly StartDate,
    DateOnly? EndDate,
    int? OccurrenceCount,
    int CompletedOccurrenceCount,
    DateOnly NextOccurrenceDate,
    RecurringBookingPlanStatus Status,
    DateTime CreatedAtUtc);

public sealed record AdminRecurringPlanSearchResponse(
    IReadOnlyList<AdminRecurringPlanSummaryResponse> Items,
    int TotalCount,
    int Page,
    int PageSize);

/// <summary>
/// The report's horizon. Both ends optional: omitting them reports the next
/// <see cref="IRecurringBookingPlanAdminService.DefaultHorizonDays"/> days
/// from today, which is the view an ops admin opening the screen wants.
/// </summary>
public sealed record AdminRecurringPlanReportRequest(DateOnly? FromDate, DateOnly? ToDate);

/// <summary>Plan count for one lifecycle status - zero-filled, so a status with no plans still appears as 0 rather than being absent from the list.</summary>
public sealed record RecurringPlanStatusCountRow(RecurringBookingPlanStatus Status, int PlanCount);

/// <summary>Active-plan count for one cadence - the standing weekly/biweekly/monthly load behind the raw plan count.</summary>
public sealed record RecurringPlanFrequencyCountRow(RecurringBookingRecurrenceFrequency Frequency, int PlanCount);

/// <summary>Recurring-origin bookings already scheduled for one date inside the horizon.</summary>
public sealed record RecurringPlanDailyVolumeRow(DateOnly SlotDate, int BookingCount);

/// <summary>
/// The admin recurring-plan report (task 299), mirroring the
/// Coupon/Commission/Nestly Coins report shape: a small set of aggregates
/// over a caller-chosen window, every one of them computed by the database.
///
/// "Upcoming occurrence volume" is deliberately reported as two different
/// numbers rather than one, because a recurring plan has two kinds of future:
/// <list type="bullet">
/// <item><see cref="UpcomingOccurrenceVolume"/> - bookings that already exist
/// (<c>Booking.RecurringBookingPlanId</c> is set, task 296's FK) and fall in
/// the horizon. This is real, committed work an ops admin can staff against.</item>
/// <item><see cref="PlansDueInHorizon"/> - active plans whose
/// <see cref="RecurringBookingPlan.NextOccurrenceDate"/> falls in the horizon
/// but which the scheduler has not reached yet. This is work that is coming
/// but is not yet a booking, and may still be skipped
/// (<see cref="RecurringBookingOccurrenceOutcome"/>).</item>
/// </list>
/// Collapsing the two into a single "expected occurrences" figure would need
/// this service to re-run <see cref="RecurringBookingPlan.PreviewUpcomingOccurrenceDates"/>
/// per plan in memory - which is both a projection presented as a fact and
/// exactly the row-by-row aggregation this report avoids.
/// </summary>
public sealed record AdminRecurringPlanReportResponse(
    int TotalPlans,
    IReadOnlyList<RecurringPlanStatusCountRow> ByStatus,
    IReadOnlyList<RecurringPlanFrequencyCountRow> ActiveByFrequency,
    DateOnly HorizonFromDate,
    DateOnly HorizonToDate,
    int PlansDueInHorizon,
    int UpcomingOccurrenceVolume,
    IReadOnlyList<RecurringPlanDailyVolumeRow> UpcomingVolumeByDate);

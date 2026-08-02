using Nestly.Application.Pricing;
using Nestly.Domain;

namespace Nestly.Application.RecurringBookings;

/// <summary>
/// Same identity/catalog/slot fields <see cref="Bookings.BookingSummaryRequest"/>
/// takes, minus <c>SlotDate</c> (replaced by <see cref="StartDate"/> plus the
/// recurrence fields, since a plan's occurrence dates are computed, not
/// picked one at a time) and minus <c>CouponCode</c> (a coupon is a one-time
/// promotional redemption; auto-reapplying the same code to every future
/// occurrence would turn a single-use promotion into a standing discount it
/// was never designed to be - task 184's scope note).
/// </summary>
public record CreateRecurringBookingPlanRequest(
    Guid ServiceId,
    Guid CityId,
    Guid AddressId,
    Guid LocalityId,
    Guid SlotWindowId,
    int Quantity,
    RecurringBookingRecurrenceFrequency Frequency,
    DayOfWeek? RecurrenceDayOfWeek,
    int? RecurrenceDayOfMonth,
    DateOnly StartDate,
    DateOnly? EndDate,
    int? OccurrenceCount,
    IReadOnlyList<AddOnSelection> AddOns);

public record RecurringBookingPlanResponse(
    Guid Id,
    Guid ServiceId,
    string ServiceName,
    Guid AddressId,
    Guid SlotWindowId,
    int Quantity,
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

/// <summary>A projected future date (not yet a real booking) or a past outcome the scheduler already recorded - see <see cref="RecurringBookingOccurrence"/>'s doc comment on why the two are computed differently.</summary>
public record UpcomingOccurrenceResponse(DateOnly ScheduledDate, bool IsProjected);

public record OccurrenceHistoryResponse(
    DateOnly ScheduledDate,
    RecurringBookingOccurrenceOutcome Outcome,
    Guid? BookingId,
    string? SkipReason,
    DateTime ProcessedAtUtc);

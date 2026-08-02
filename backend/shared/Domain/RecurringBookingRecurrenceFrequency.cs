namespace Nestly.Domain;

/// <summary>
/// How often a <see cref="RecurringBookingPlan"/> repeats (PRODUCT-ENHANCEMENTS.md
/// section 2: "weekly/biweekly/monthly + day/time"). The "time" half of that
/// pair is deliberately not a separate field here - it is the plan's
/// <see cref="RecurringBookingPlan.SlotWindowId"/>, the same slot-window
/// concept every booking already uses, rather than a raw wall-clock time that
/// would need its own re-validation against slot policy at occurrence time.
/// </summary>
public enum RecurringBookingRecurrenceFrequency
{
    /// <summary>Every 7 days, on <see cref="RecurringBookingPlan.RecurrenceDayOfWeek"/>.</summary>
    Weekly,

    /// <summary>Every 14 days, on <see cref="RecurringBookingPlan.RecurrenceDayOfWeek"/>.</summary>
    Biweekly,

    /// <summary>Every calendar month, on <see cref="RecurringBookingPlan.RecurrenceDayOfMonth"/> (clamped to the shorter month, e.g. 31 -> 28/29 in February).</summary>
    Monthly
}

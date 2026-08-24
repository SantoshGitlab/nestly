namespace Nestly.Application.Abstractions.Time;

/// <summary>
/// The platform's business wall-clock.
///
/// Slot windows (<c>SlotWindow.StartTime</c>/<c>EndTime</c>) and slot dates
/// (<c>Booking.SlotDate</c>) are stored as local business time - "the 09:00
/// to 13:00 window on the 6th" - with no offset attached anywhere in the
/// schema. Comparing those values against <see cref="TimeProvider.GetUtcNow"/>
/// therefore compares two different clocks: in IST (UTC+05:30) the UTC
/// instant is 5.5 hours behind the business wall clock, which made every
/// cutoff check that much too lenient and left windows that had already
/// finished still bookable.
///
/// Every comparison between a stored slot time and "now" must go through
/// this abstraction: either compare wall-clock to wall-clock
/// (<see cref="Now"/>, <see cref="Today"/>) or lift the stored local time to
/// a real instant (<see cref="ToUtc"/>) before comparing against UTC.
/// </summary>
public interface IBusinessClock
{
    /// <summary>Current wall-clock time in the configured business timezone.</summary>
    DateTime Now { get; }

    /// <summary>Today's calendar date in the configured business timezone.</summary>
    DateOnly Today { get; }

    /// <summary>
    /// Lifts a stored business-local date + time-of-day (a slot window start,
    /// for example) to the UTC instant it actually occurs at, so it can be
    /// compared with <see cref="TimeProvider.GetUtcNow"/>.
    /// </summary>
    DateTime ToUtc(DateOnly date, TimeSpan timeOfDay);

    /// <summary>
    /// The inverse of <see cref="ToUtc"/>: converts a real UTC instant (a
    /// <c>BookingProviderAssignment.CompletedAt</c>, for example) to the
    /// business-local wall-clock date and time it falls on, so it can be
    /// compared against a stored slot window's own local start/end times.
    /// </summary>
    DateTime ToBusinessLocal(DateTime utcInstant);
}

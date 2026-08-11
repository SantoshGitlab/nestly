/**
 * Local calendar-date helpers.
 *
 * Every date the API takes as `YYYY-MM-DD` means a *calendar day*, not an
 * instant. `new Date().toISOString().slice(0, 10)` is the obvious way to
 * produce one and is wrong everywhere east of Greenwich: toISOString converts
 * to UTC first, so in IST (UTC+05:30) any local time before 05:30 yields the
 * previous day. That shipped as a real defect in the slot picker, the booking
 * summary, the reschedule screen and the recurring-booking form — each
 * defaulted a customer to yesterday's date for the first five and a half
 * hours of every day.
 *
 * Use these instead of touching toISOString for anything calendar-shaped.
 */

import { RecurringBookingRecurrenceFrequency } from "@/lib/types";

/** A `Date`'s local calendar date as `YYYY-MM-DD`. */
export function toLocalIsoDate(date: Date): string {
  const year = date.getFullYear();
  const month = `${date.getMonth() + 1}`.padStart(2, "0");
  const day = `${date.getDate()}`.padStart(2, "0");
  return `${year}-${month}-${day}`;
}

/** Today, as a local `YYYY-MM-DD` calendar date. */
export function todayIsoDate(): string {
  return toLocalIsoDate(new Date());
}

/**
 * `offsetDays` from today as a local `YYYY-MM-DD`. Negative goes backwards.
 *
 * Uses `setDate`, which handles month and year rollover and — unlike adding
 * milliseconds — stays correct across a daylight-saving transition, where a
 * day is not always 24 hours.
 */
export function isoDateOffsetFromToday(offsetDays: number): string {
  const date = new Date();
  date.setDate(date.getDate() + offsetDays);
  return toLocalIsoDate(date);
}

/**
 * The date exactly one recurrence interval after `isoDate`.
 *
 * Mirrors `Nestly.Domain.RecurringBookingPlan`'s own arithmetic so the date
 * this app shows ("first repeat visit: …") is the date the server will
 * actually compute: weekly is +7 days, biweekly +14, monthly the same
 * day-of-month next month clamped to that month's length — 31 Jan becomes 28
 * Feb, matching `RecurringBookingPlan.ClampToMonth`.
 *
 * Used by the booking summary's "repeat this booking" opt-in (task 298) to
 * derive the plan's start date. It must be one full interval after the booking
 * being placed, not the booking's own date: the plan's first occurrence is
 * `NextOccurrenceOnOrAfter(startDate)` server-side, so starting it on the
 * booked date would have the scheduler book a second, duplicate visit for the
 * day the customer is already paying for.
 */
export function addRecurrenceInterval(
  isoDate: string,
  frequency: RecurringBookingRecurrenceFrequency,
): string {
  // Local midnight, not `new Date(isoDate)` — a bare YYYY-MM-DD parses as UTC
  // and shifts the day for anyone behind UTC.
  const date = new Date(`${isoDate}T00:00:00`);

  if (frequency === RecurringBookingRecurrenceFrequency.Monthly) {
    const dayOfMonth = date.getDate();
    // Step via the 1st of next month rather than `setMonth(m + 1)`: on the
    // 31st, setMonth rolls a 30-day month over into the month *after* next
    // (31 Mar -> 1 May), which silently skips a whole visit.
    const target = new Date(date.getFullYear(), date.getMonth() + 1, 1);
    // Day 0 of the following month is the last day of `target`'s month.
    const daysInTargetMonth = new Date(target.getFullYear(), target.getMonth() + 1, 0).getDate();
    target.setDate(Math.min(dayOfMonth, daysInTargetMonth));
    return toLocalIsoDate(target);
  }

  date.setDate(
    date.getDate() + (frequency === RecurringBookingRecurrenceFrequency.Biweekly ? 14 : 7),
  );
  return toLocalIsoDate(date);
}

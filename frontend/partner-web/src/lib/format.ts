/**
 * Display formatters shared by the partner screens.
 *
 * Money and timestamps were previously formatted ad hoc at every call site
 * (`₹${n.toFixed(2)}`, `new Date(s).toLocaleString()`), which drifted between
 * the ledger, the payout list and the payout detail for the same value.
 *
 * NOTE: nothing here produces a `YYYY-MM-DD` calendar date. That is
 * deliberate - use lib/date.ts for those. `toISOString().slice(0, 10)`
 * converts to UTC first and returns the previous day in IST before 05:30.
 */

const INR = new Intl.NumberFormat("en-IN", {
  style: "currency",
  currency: "INR",
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
});

/**
 * Rupee amount with Indian digit grouping (₹1,23,456.00).
 *
 * Always pair the output with the `.nums` class wherever amounts stack into a
 * column, so the digits line up as the values change.
 */
export function formatInr(amount: number | null | undefined): string {
  if (amount === null || amount === undefined || Number.isNaN(amount)) return "—";
  return INR.format(amount);
}

/**
 * Signed rupee amount for ledger rows. A debit is rendered as a negative so a
 * penalty can never be mistaken for a credit of the same size.
 */
export function formatSignedInr(amount: number, isDebit: boolean): string {
  const magnitude = INR.format(Math.abs(amount));
  return isDebit ? `−${magnitude}` : `+${magnitude}`;
}

/** An instant as a short local date, e.g. "3 Aug 2026". */
export function formatDate(value: string | null | undefined): string {
  if (!value) return "—";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "—";
  return date.toLocaleDateString(undefined, { day: "numeric", month: "short", year: "numeric" });
}

/** An instant as a local date and time, e.g. "3 Aug 2026, 14:30". */
export function formatDateTime(value: string | null | undefined): string {
  if (!value) return "—";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "—";
  return date.toLocaleString(undefined, {
    day: "numeric",
    month: "short",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
}

/**
 * A `YYYY-MM-DD` calendar date (as the API sends it) rendered for humans.
 * Parsed as local parts rather than through `new Date(string)`, which treats a
 * bare date as UTC midnight and so shows the previous day in IST.
 */
export function formatIsoDate(value: string | null | undefined): string {
  if (!value) return "—";
  const [year, month, day] = value.split("-").map(Number);
  if (!year || !month || !day) return value;
  return new Date(year, month - 1, day).toLocaleDateString(undefined, {
    day: "numeric",
    month: "short",
    year: "numeric",
  });
}

/** "09:00:00" (a TimeSpan over the wire) as "09:00". */
export function formatTime(value: string | null | undefined): string {
  if (!value) return "—";
  return value.slice(0, 5);
}

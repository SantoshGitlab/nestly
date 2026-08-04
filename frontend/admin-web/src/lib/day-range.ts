import { toLocalIsoDate } from "./date";

/**
 * Local calendar day <-> UTC instant conversions for the admin's date-range
 * controls.
 *
 * `<input type="date">` gives a calendar day; several of these endpoints take
 * a full UTC instant. The obvious bridge — `${date}T00:00:00.000Z` — declares
 * the admin's local day boundary to be a UTC one, which in IST shifts the
 * window 5h30m: a report "from 4 August" silently starts at 05:30 IST on the
 * 4th and includes 18:30–24:00 IST on the 3rd, and a coupon "valid to
 * 4 August" stays live until 05:29 IST on the 5th. Both boundaries are built
 * from local-time components instead, so the window is the day the admin
 * picked.
 *
 * Lives beside `date.ts` rather than inside it: that file is byte-identical
 * across the three apps, and this pair is only needed by admin-web's filters.
 */

function localDayParts(isoDate: string): { year: number; month: number; day: number } | null {
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(isoDate);
  if (!match) return null;
  return { year: Number(match[1]), month: Number(match[2]), day: Number(match[3]) };
}

/** First instant of the given local calendar day, as an ISO UTC string. */
export function startOfLocalDayUtc(isoDate: string): string | null {
  const parts = localDayParts(isoDate);
  if (!parts) return null;
  return new Date(parts.year, parts.month - 1, parts.day, 0, 0, 0, 0).toISOString();
}

/** Last instant of the given local calendar day, as an ISO UTC string. */
export function endOfLocalDayUtc(isoDate: string): string | null {
  const parts = localDayParts(isoDate);
  if (!parts) return null;
  return new Date(parts.year, parts.month - 1, parts.day, 23, 59, 59, 999).toISOString();
}

/** A stored UTC instant back to the local `yyyy-mm-dd` a date input expects. */
export function utcToLocalDateInput(iso: string | null | undefined): string {
  if (!iso) return "";
  const parsed = new Date(iso);
  return Number.isNaN(parsed.getTime()) ? "" : toLocalIsoDate(parsed);
}

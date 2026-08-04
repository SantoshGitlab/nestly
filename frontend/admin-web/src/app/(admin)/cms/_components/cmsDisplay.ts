import { formatDateTime } from "@/components/data-table";
import { CmsContentStatus, CmsPlacement } from "@/lib/cms-types";
import { toLocalIsoDate } from "@/lib/date";

/** Shared placement labels/options for the page, banner, and FAQ forms/tables (SRS 12.16.1, task 124f) - one taxonomy, not duplicated per screen. */
export const PLACEMENT_OPTIONS = [
  { value: String(CmsPlacement.Home), label: "Home" },
  { value: String(CmsPlacement.CategoryPage), label: "Category page" },
  { value: String(CmsPlacement.Promotional), label: "Promotional" },
  { value: String(CmsPlacement.Footer), label: "Footer" },
  { value: String(CmsPlacement.General), label: "General" },
] as const;

/** Filter variants of the two CMS taxonomies, shared by all three list screens. */
export const PLACEMENT_FILTER_OPTIONS = [
  { value: "", label: "All placements" },
  ...PLACEMENT_OPTIONS,
] as const;

export const STATUS_FILTER_OPTIONS = [
  { value: "", label: "All statuses" },
  { value: String(CmsContentStatus.Draft), label: "Draft" },
  { value: String(CmsContentStatus.Published), label: "Published" },
] as const;

/**
 * The publish window as one line. Pages, banners and FAQs each carried a
 * byte-identical private copy of this; a schedule must read the same on all
 * three screens, so it lives with the rest of the CMS display vocabulary.
 */
export function formatSchedule(item: {
  publishStartUtc: string | null;
  publishEndUtc: string | null;
}): string {
  if (!item.publishStartUtc && !item.publishEndUtc) return "Always";
  const start = item.publishStartUtc ? formatDateTime(item.publishStartUtc) : "now";
  const end = item.publishEndUtc ? formatDateTime(item.publishEndUtc) : "indefinitely";
  return `${start} – ${end}`;
}

export function formatPlacement(placement: CmsPlacement): string {
  switch (placement) {
    case CmsPlacement.Home:
      return "Home";
    case CmsPlacement.CategoryPage:
      return "Category page";
    case CmsPlacement.Promotional:
      return "Promotional";
    case CmsPlacement.Footer:
      return "Footer";
    default:
      return "General";
  }
}

/**
 * `<input type="datetime-local">` value ("yyyy-mm-ddThh:mm") to a UTC instant.
 *
 * This used to append `Z` to the raw control value, which declares the
 * admin's local wall-clock reading to be UTC. In IST that pushed every
 * publish window 5h30m late — and because `CmsPagesTable` renders the stored
 * instant with `toLocaleString()`, the same schedule read "09:00" in the form
 * and "14:30" in the list directly beneath it. The control's value is local
 * time, so it is parsed as local time (which is what `new Date` does for a
 * date-time string carrying no offset) and converted properly.
 *
 * Empty string means "no schedule boundary".
 */
export function datetimeLocalToUtc(value: string): string | null {
  if (value === "") return null;
  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime()) ? null : parsed.toISOString();
}

/** A UTC instant back to the local "yyyy-mm-ddThh:mm" the control expects, or "" when unset. */
export function utcToDatetimeLocal(iso: string | null): string {
  if (!iso) return "";
  const parsed = new Date(iso);
  if (Number.isNaN(parsed.getTime())) return "";
  const hours = `${parsed.getHours()}`.padStart(2, "0");
  const minutes = `${parsed.getMinutes()}`.padStart(2, "0");
  // `toLocalIsoDate` rather than slicing an ISO string: the latter is the UTC
  // calendar date and rolls back a day for local times before 05:30 in IST.
  return `${toLocalIsoDate(parsed)}T${hours}:${minutes}`;
}

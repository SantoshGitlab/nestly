import { CmsPlacement } from "@/lib/cms-types";

/** Shared placement labels/options for the page, banner, and FAQ forms/tables (SRS 12.16.1, task 124f) - one taxonomy, not duplicated per screen. */
export const PLACEMENT_OPTIONS = [
  { value: String(CmsPlacement.Home), label: "Home" },
  { value: String(CmsPlacement.CategoryPage), label: "Category page" },
  { value: String(CmsPlacement.Promotional), label: "Promotional" },
  { value: String(CmsPlacement.Footer), label: "Footer" },
  { value: String(CmsPlacement.General), label: "General" },
] as const;

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

/** `<input type="datetime-local">` yields "yyyy-mm-ddThh:mm" with no timezone - treated as UTC directly (appending "Z"), same convention CouponForm's date-only helpers use for the day boundary. Empty string means "no schedule boundary". */
export function datetimeLocalToUtc(value: string): string | null {
  return value === "" ? null : `${value}:00.000Z`;
}

/** An ISO instant to `<input type="datetime-local">`'s "yyyy-mm-ddThh:mm" value, or "" when unset. */
export function utcToDatetimeLocal(iso: string | null): string {
  return iso ? iso.slice(0, 16) : "";
}

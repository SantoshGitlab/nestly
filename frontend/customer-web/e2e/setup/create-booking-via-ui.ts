import type { Page } from "@playwright/test";
import { expect } from "@playwright/test";
import type { CatalogFixture } from "./seed-catalog";

/**
 * Drives the real booking summary -> payment -> success flow (covers 140b)
 * and returns the resulting booking id, so 140c/140d can start from a
 * genuinely-created Confirmed booking instead of re-deriving one from
 * scratch or reaching into the database.
 */
export async function createBookingViaUi(page: Page, fixture: CatalogFixture): Promise<string> {
  await page.goto(`/booking/summary?serviceSlug=${fixture.serviceSlug}`);

  await expect(page.getByRole("heading", { name: "Review your booking" })).toBeVisible();

  // Address defaults to the customer's default saved address automatically.
  await expect(page.locator('input[name="address"]').first()).toBeChecked({ timeout: 15_000 });

  // Date defaults to today, but a same-day slot window can already be
  // "in the past" for slot-cutoff purposes depending on what time of day
  // the suite runs (SlotAvailabilityService filters on window.StartTime >=
  // now + cutoff, not just the calendar date) - tomorrow is always safe
  // since its 00:00 window start is never in the past. The date strip's
  // second button (index 1) is always tomorrow (SlotPicker's upcomingDates
  // starts at today).
  const dateStrip = page.locator("h3", { hasText: "Date" }).locator("xpath=following-sibling::div[1]//button");
  await expect(dateStrip.nth(1)).toBeVisible({ timeout: 15_000 });
  await dateStrip.nth(1).click();

  const slotButton = page.getByRole("button", { name: /E2E Anytime/ });
  await expect(slotButton).toBeVisible({ timeout: 15_000 });
  await slotButton.click();
  await expect(slotButton).toHaveAttribute("aria-pressed", "true");

  const proceedButton = page.getByRole("button", { name: "Proceed to book" });
  await expect(proceedButton).toBeEnabled({ timeout: 15_000 });
  await proceedButton.click();

  await page.waitForURL(/\/booking\/payment\//, { timeout: 15_000 });
  const bookingId = page.url().match(/\/booking\/payment\/([^/?]+)/)?.[1];
  if (!bookingId) throw new Error(`Could not extract booking id from URL: ${page.url()}`);

  const payButton = page.getByRole("button", { name: /Pay ₹.*\(Sandbox\)/ });
  await expect(payButton).toBeVisible({ timeout: 15_000 });
  await payButton.click();

  await page.waitForURL(new RegExp(`/booking/success/${bookingId}`), { timeout: 15_000 });
  await expect(page.getByRole("heading", { name: "Booking placed!" })).toBeVisible();

  return bookingId;
}

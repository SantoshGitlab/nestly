import type { Page } from "@playwright/test";
import { expect } from "@playwright/test";
import type { CatalogFixture } from "./seed-catalog";

/**
 * The "repeat this booking" opt-in on the summary page (task 298), when the
 * caller wants the booking to also set up a standing plan. `frequency` is the
 * picker button's visible label.
 */
export interface RepeatOptIn {
  frequency: "Every week" | "Every 2 weeks" | "Every month";
  visits: number;
}

/** Which day of the date strip this helper books - see the comment on the click below. */
export const BOOKED_DATE_OFFSET_DAYS = 2;

/**
 * Drives the real booking summary -> payment -> success flow (covers 140b)
 * and returns the resulting booking id, so 140c/140d can start from a
 * genuinely-created Confirmed booking instead of re-deriving one from
 * scratch or reaching into the database.
 */
export async function createBookingViaUi(
  page: Page,
  fixture: CatalogFixture,
  repeat?: RepeatOptIn,
): Promise<string> {
  await page.goto(`/booking/summary?serviceSlug=${fixture.serviceSlug}`);

  await expect(page.getByRole("heading", { name: "Review your booking" })).toBeVisible();

  // Address defaults to the customer's default saved address automatically.
  await expect(page.locator('input[name="address"]').first()).toBeChecked({ timeout: 15_000 });

  // Date defaults to today, but a same-day slot window can already be
  // "in the past" for slot-cutoff purposes depending on what time of day
  // the suite runs (SlotAvailabilityService filters on window.StartTime >=
  // now + cutoff, not just the calendar date). The date strip's buttons are
  // today, tomorrow, ... (SlotPicker's upcomingDates starts at today).
  //
  // Index 2 (the day after tomorrow) rather than 1: the seeded E2E Anytime
  // window starts at 00:00, so *tomorrow's* slot is only minutes away in real
  // time when the suite runs late in the evening - which put every booking it
  // creates inside the 2-hour reschedule cutoff and made 140c fail purely on
  // wall-clock time. That was masked until the slot engine's cutoff maths was
  // corrected to compare business-local times against business-local now
  // rather than against UTC (see IBusinessClock): in IST the old comparison
  // reported every slot as 5.5 hours further away than it really was. Two days
  // out keeps every booking this helper creates comfortably inside every
  // policy window at any hour.
  const dateStrip = page.locator("h3", { hasText: "Date" }).locator("xpath=following-sibling::div[1]//button");
  await expect(dateStrip.nth(BOOKED_DATE_OFFSET_DAYS)).toBeVisible({ timeout: 15_000 });
  await dateStrip.nth(BOOKED_DATE_OFFSET_DAYS).click();

  const slotButton = page.getByRole("button", { name: /E2E Anytime/ });
  await expect(slotButton).toBeVisible({ timeout: 15_000 });
  await slotButton.click();
  await expect(slotButton).toHaveAttribute("aria-pressed", "true");

  if (repeat) {
    await page.getByRole("checkbox", { name: "Repeat this booking" }).check();
    const frequencyButton = page.getByRole("radio", { name: repeat.frequency });
    await expect(frequencyButton).toBeVisible({ timeout: 15_000 });
    await frequencyButton.click();
    await expect(frequencyButton).toHaveAttribute("aria-checked", "true");

    const visitsField = page.getByLabel("Number of repeat visits");
    await visitsField.fill(String(repeat.visits));
  }

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

import { test, expect } from "@playwright/test";
import type { Page } from "@playwright/test";
import { loadFixture, authenticateAsSeededCustomer } from "./setup/auth";
import { BOOKED_DATE_OFFSET_DAYS, createBookingViaUi } from "./setup/create-booking-via-ui";

/**
 * Task 298: "repeat this booking" on the booking flow, and the subscriptions
 * management screen it feeds.
 *
 * The assertion that earns this file is the *date*: the plan must start one
 * full interval after the booking being placed, never on the booked date
 * itself. Starting it on the booked date is the obvious implementation and is
 * wrong — the plan's first occurrence is `NextOccurrenceOnOrAfter(startDate)`
 * server-side, so the scheduler would book a second, duplicate visit for the
 * very day the customer is already paying for, and nothing else in this suite
 * would notice.
 */

/** The seeded booking is placed BOOKED_DATE_OFFSET_DAYS out; a weekly plan repeats 7 days after that. */
const WEEKLY_INTERVAL_DAYS = 7;

/**
 * Formats a date the way `formatCalendarDate` does, but *in the browser*, so
 * the expectation resolves against Chromium's locale rather than Node's. The
 * two are not guaranteed to agree, and a mismatch would fail this test for a
 * reason that has nothing to do with the behaviour under test.
 */
async function formatInPage(page: Page, offsetDays: number): Promise<string> {
  return page.evaluate((days) => {
    const date = new Date();
    date.setHours(0, 0, 0, 0);
    date.setDate(date.getDate() + days);
    return date.toLocaleDateString(undefined, {
      weekday: "short",
      day: "numeric",
      month: "short",
      year: "numeric",
    });
  }, offsetDays);
}

test.describe("Repeat this booking (recurring plan opt-in)", () => {
  test("opting in on the booking flow creates a plan starting one interval later, manageable from the subscriptions screen", async ({
    page,
  }) => {
    const fixture = loadFixture();
    await authenticateAsSeededCustomer(page, fixture);

    // Placed on today + BOOKED_DATE_OFFSET_DAYS, repeating weekly from a week
    // after that - NOT from the booked date.
    await createBookingViaUi(page, fixture, { frequency: "Every week", visits: 3 });

    const expectedNextVisit = await formatInPage(
      page,
      BOOKED_DATE_OFFSET_DAYS + WEEKLY_INTERVAL_DAYS,
    );
    const bookedDate = await formatInPage(page, BOOKED_DATE_OFFSET_DAYS);

    await page.goto("/recurring-bookings");
    await expect(page.getByRole("heading", { name: "Recurring bookings" })).toBeVisible();

    // Plans come back most-recently-created first, so the plan this test just
    // made is the first row even on a re-run against a dirty database.
    //
    // Filtered on the card's own heading rather than taken as the page's first
    // listitem: the page's BannerBreadcrumb is a list too and its items come
    // first in the DOM, so a bare .first() picks "Home" out of the breadcrumb.
    // Each plan card carries an h2 with the service name; neither the
    // breadcrumb items nor the per-card nested detail list does.
    const planCard = page
      .getByRole("listitem")
      .filter({ has: page.getByRole("heading", { level: 2 }) })
      .first();
    await expect(planCard.getByRole("heading", { name: fixture.serviceName })).toBeVisible({
      timeout: 15_000,
    });
    await expect(planCard).toContainText("Every week");
    await expect(planCard).toContainText("Active");

    // The whole point: the next visit is a week after the booking, not on it.
    // (The "Started" row carries the same date, since the plan's start date
    // *is* its first occurrence - hence a count rather than a bare contains.)
    await expect(planCard.getByText(expectedNextVisit)).toHaveCount(2);
    await expect(planCard).not.toContainText(bookedDate);

    // Bounded by the 3 repeat visits asked for, not by the booking itself.
    await expect(planCard).toContainText("0 of 3");
    await planCard.getByRole("button", { name: "Show upcoming" }).click();
    // Exact: the card also carries a "Next visit" detail row, and a substring
    // match picks up its wrapper as well once this section is expanded.
    await expect(planCard.getByText("Next visits", { exact: true })).toBeVisible({ timeout: 15_000 });
    await expect(planCard.locator("li").filter({ hasText: "(projected)" })).toHaveCount(3);

    // Pause / resume / cancel - the management actions the row calls for.
    await planCard.getByRole("button", { name: "Pause" }).click();
    await expect(planCard).toContainText("Paused", { timeout: 15_000 });

    await planCard.getByRole("button", { name: "Resume" }).click();
    await expect(planCard).toContainText("Active", { timeout: 15_000 });

    await planCard.getByRole("button", { name: "Cancel plan" }).click();
    await page.getByRole("button", { name: "Yes, cancel plan" }).click();
    await expect(planCard).toContainText("Cancelled", { timeout: 15_000 });
  });

  test("the frequency picker restates the first repeat date for each frequency", async ({
    page,
  }) => {
    const fixture = loadFixture();
    await authenticateAsSeededCustomer(page, fixture);

    await page.goto(`/booking/summary?serviceSlug=${fixture.serviceSlug}`);
    await expect(page.getByRole("heading", { name: "Review your booking" })).toBeVisible();

    // No slot picked, so the summary's date is today - the repeat card is
    // deliberately readable before the rest of the booking is complete.
    await page.getByRole("checkbox", { name: "Repeat this booking" }).check();

    const repeatCard = page
      .locator("section")
      .filter({ has: page.getByRole("heading", { name: "Repeat this booking" }) });

    for (const [label, expected] of [
      ["Every week", await formatInPage(page, 7)],
      ["Every 2 weeks", await formatInPage(page, 14)],
    ] as const) {
      await repeatCard.getByRole("radio", { name: label }).click();
      await expect(repeatCard).toContainText(`First repeat visit: ${expected}`);
    }

    // Monthly is the one with real arithmetic in it: same day-of-month next
    // month, clamped to that month's length (31 Jan -> 28 Feb). Computed here
    // by a different route - walk forward a day at a time until the month
    // rolls over - so this expectation is not the implementation restated.
    const expectedMonthly = await page.evaluate(() => {
      const start = new Date();
      start.setHours(0, 0, 0, 0);
      const cursor = new Date(start);
      while (cursor.getMonth() === start.getMonth()) cursor.setDate(cursor.getDate() + 1);
      const lastDayOfTargetMonth = new Date(
        cursor.getFullYear(),
        cursor.getMonth() + 1,
        0,
      ).getDate();
      cursor.setDate(Math.min(start.getDate(), lastDayOfTargetMonth));
      return cursor.toLocaleDateString(undefined, {
        weekday: "short",
        day: "numeric",
        month: "short",
        year: "numeric",
      });
    });

    await repeatCard.getByRole("radio", { name: "Every month" }).click();
    await expect(repeatCard).toContainText(`First repeat visit: ${expectedMonthly}`);
  });
});

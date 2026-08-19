import { test, expect } from "@playwright/test";
import { authenticateAsSeededAdmin, loadFixture } from "./setup/auth";

/**
 * Task 317: bookings list/search -> detail (SRS 12.11.1-3, task 116). Filters
 * on the exact seeded booking id (dev database already carries bookings from
 * prior customer-web E2E runs / manual QA - see e2e/setup/seed-admin.ts) so
 * the search narrows to exactly one row regardless of what else is in the
 * table, then follows it into the detail page.
 */
test.describe("Bookings list and detail", () => {
  test("searches by booking ID and opens the matching booking's detail page", async ({ page }) => {
    const fixture = loadFixture();
    await authenticateAsSeededAdmin(page, fixture);

    await page.goto("/bookings");
    await expect(page.getByRole("heading", { name: "Bookings" })).toBeVisible();

    await page.getByLabel("Booking ID").fill(fixture.sampleBookingId);
    await page.getByRole("button", { name: "Search" }).click();

    // Matched by href rather than by customer name: the results table keeps
    // showing the previous (unfiltered) page via react-query's
    // `placeholderData` while the filtered request is in flight, and several
    // seeded bookings share the customer-web E2E suite's fixed test-customer
    // name - a name-based locator is briefly ambiguous (strict-mode
    // violation, which Playwright does not retry through) during that
    // window. The href is unique to this booking from the first render.
    // `:visible` because the responsive DataTable renders every row twice -
    // once for the desktop table and once for the stacked mobile layout - and
    // hides one with CSS, so the bare href matches two elements.
    const customerLink = page.locator(`a[href="/bookings/${fixture.sampleBookingId}"]:visible`);
    await expect(customerLink).toBeVisible({ timeout: 15_000 });
    await expect(customerLink).toHaveText(fixture.sampleBookingCustomerName);
    await customerLink.click();

    await page.waitForURL(new RegExp(`/bookings/${fixture.sampleBookingId}`));
    await expect(page.getByRole("heading", { name: fixture.sampleBookingCustomerName })).toBeVisible();
    await expect(page.getByText(`Booking ${fixture.sampleBookingId}`)).toBeVisible();
    await expect(page.getByText("Status timeline")).toBeVisible();
  });
});

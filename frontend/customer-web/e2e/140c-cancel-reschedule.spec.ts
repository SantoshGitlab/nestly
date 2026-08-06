import { test, expect } from "@playwright/test";
import { loadFixture, authenticateAsSeededCustomer } from "./setup/auth";
import { createBookingViaUi } from "./setup/create-booking-via-ui";

/**
 * Task 140c: cancellation and reschedule flows (SRS 33 UAT flow 3). Each
 * test books its own fresh booking (rather than sharing one across tests)
 * since cancelling a booking makes it ineligible for reschedule and vice
 * versa - BookingLifecycle only allows one terminal-ish transition per
 * booking from Confirmed.
 */
test.describe("Cancellation and reschedule", () => {
  test("cancels a confirmed booking and shows the refund outcome", async ({ page }) => {
    const fixture = loadFixture();
    await authenticateAsSeededCustomer(page, fixture);

    const bookingId = await createBookingViaUi(page, fixture);

    await page.goto(`/bookings/${bookingId}`);
    await page.getByRole("link", { name: "Cancel booking" }).click();
    await page.waitForURL(new RegExp(`/bookings/${bookingId}/cancel`));

    await expect(page.getByRole("heading", { name: "Cancel booking" })).toBeVisible();
    await expect(page.getByText("Refund amount")).toBeVisible({ timeout: 15_000 });

    await page.locator("#cancel-reason").fill("E2E test: no longer needed.");
    await page.getByRole("button", { name: "Confirm cancellation" }).click();

    await expect(page.getByRole("heading", { name: "Booking cancelled" })).toBeVisible({ timeout: 15_000 });
    await expect(page.getByText("Refund amount")).toBeVisible();
    await expect(page.getByText("Within free cancellation window")).toBeVisible();

    await page.getByRole("button", { name: "Back to booking" }).click();
    await page.waitForURL(new RegExp(`/bookings/${bookingId}$`));
  });

  test("reschedules a confirmed booking to a new slot", async ({ page }) => {
    const fixture = loadFixture();
    await authenticateAsSeededCustomer(page, fixture);

    const bookingId = await createBookingViaUi(page, fixture);

    await page.goto(`/bookings/${bookingId}`);
    await page.getByRole("link", { name: "Reschedule booking" }).click();
    await page.waitForURL(new RegExp(`/bookings/${bookingId}/reschedule`));

    await expect(page.getByRole("heading", { name: "Reschedule booking" })).toBeVisible();

    // Move three days out (index 3) rather than two, where the booking
    // already is - a same-day-window slot can go stale mid-run depending
    // on wall-clock time, same reasoning as create-booking-via-ui.ts.
    const dateStrip = page.locator("h3", { hasText: "Date" }).locator("xpath=following-sibling::div[1]//button");
    await expect(dateStrip.nth(3)).toBeVisible({ timeout: 15_000 });
    await dateStrip.nth(3).click();

    const slotButton = page.getByRole("button", { name: /E2E Anytime/ });
    await expect(slotButton).toBeVisible({ timeout: 15_000 });
    await slotButton.click();
    await expect(slotButton).toHaveAttribute("aria-pressed", "true");

    await page.locator("#reschedule-reason").fill("E2E test: prefer a different day.");

    const confirmButton = page.getByRole("button", { name: "Confirm reschedule" });
    await expect(confirmButton).toBeEnabled();
    await confirmButton.click();

    await page.waitForURL(new RegExp(`/bookings/${bookingId}$`), { timeout: 15_000 });
    await expect(page.getByRole("heading", { name: fixture.serviceName })).toBeVisible();
  });
});

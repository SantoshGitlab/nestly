import { test, expect } from "@playwright/test";
import { authenticateAsSeededAdmin, loadFixture } from "./setup/auth";

/**
 * Task 317: dashboard KPIs and sidebar navigation (SRS 12.3, task 100).
 * Uses a pre-authenticated session (see e2e/setup/auth.ts) since the sign-in
 * flow itself is covered by 317a-login.spec.ts.
 */
test.describe("Dashboard", () => {
  test("loads KPI tiles for the seeded Super Admin", async ({ page }) => {
    const fixture = loadFixture();
    await authenticateAsSeededAdmin(page, fixture);

    await page.goto("/dashboard");
    await expect(page.getByRole("heading", { name: "Dashboard" })).toBeVisible();

    // KPI row renders once the query resolves - each StatTile has a label.
    // Scoped to the "Key metrics" region: the sidebar also has a "Bookings"
    // nav link, which would otherwise make the text locator ambiguous.
    const kpis = page.getByLabel("Key metrics");
    await expect(kpis.getByText("Bookings", { exact: true })).toBeVisible({ timeout: 15_000 });
    await expect(kpis.getByText("Cancellations", { exact: true })).toBeVisible();
  });

  test("navigates to Bookings via the sidebar", async ({ page }) => {
    const fixture = loadFixture();
    await authenticateAsSeededAdmin(page, fixture);

    await page.goto("/dashboard");
    await page.getByRole("link", { name: "Bookings", exact: true }).click();

    await page.waitForURL(/\/bookings$/);
    await expect(page.getByRole("heading", { name: "Bookings" })).toBeVisible();
  });
});

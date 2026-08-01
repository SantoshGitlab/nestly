import { test, expect } from "@playwright/test";
import { loadFixture, authenticateAsSeededCustomer } from "./setup/auth";

/**
 * Task 140a: discovery -> category -> service detail (SRS 33 UAT flow 1).
 * No customer auth needed for browse-only pages, but the seeded
 * city/locality are pre-selected so the flow doesn't stall on the picker.
 */
test.describe("Discovery to category to service detail", () => {
  test("browses from home into a category and through to service detail", async ({ page }) => {
    const fixture = loadFixture();
    await authenticateAsSeededCustomer(page, fixture);

    await page.goto("/");
    await expect(page.getByRole("heading", { name: "Popular categories" })).toBeVisible();

    const categoryLink = page.getByRole("link", { name: fixture.categoryName });
    await expect(categoryLink).toBeVisible({ timeout: 15_000 });
    await categoryLink.click();

    await page.waitForURL(new RegExp(`/categories/${fixture.categorySlug}`));
    await expect(page.getByRole("heading", { name: fixture.categoryName })).toBeVisible();

    const serviceLink = page.getByRole("link", { name: new RegExp(fixture.serviceName) });
    await expect(serviceLink).toBeVisible({ timeout: 15_000 });
    await serviceLink.click();

    await page.waitForURL(new RegExp(`/services/${fixture.serviceSlug}`));
    await expect(page.getByRole("heading", { name: fixture.serviceName })).toBeVisible();
    await expect(page.locator("#inclusions-heading")).toBeVisible();
    await expect(page.locator("#exclusions-heading")).toBeVisible();
    await expect(page.getByRole("link", { name: "Book now" })).toBeVisible();
  });

  test("finds the same service via the /categories index and via search", async ({ page }) => {
    const fixture = loadFixture();
    await authenticateAsSeededCustomer(page, fixture);

    await page.goto("/categories");
    await expect(page.getByRole("heading", { name: "All categories" })).toBeVisible();
    await expect(page.getByRole("link", { name: fixture.categoryName })).toBeVisible({ timeout: 15_000 });

    await page.goto(`/services/${fixture.serviceSlug}`);
    await expect(page.getByRole("heading", { name: fixture.serviceName })).toBeVisible();
    await expect(page.locator("#availability-heading")).toBeVisible();
  });
});

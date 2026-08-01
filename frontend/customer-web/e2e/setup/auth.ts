import fs from "node:fs";
import path from "node:path";
import type { Page } from "@playwright/test";
import type { CatalogFixture } from "./seed-catalog";

export function loadFixture(): CatalogFixture {
  return JSON.parse(fs.readFileSync(path.join(__dirname, "fixture.json"), "utf-8"));
}

/**
 * Pre-authenticates the customer session and pre-selects the seeded
 * city/locality, before any navigation. Tokens live in sessionStorage
 * (frontend/customer-web/src/lib/auth.ts) so Playwright's storageState
 * mechanism (localStorage/cookies only) can't carry them - addInitScript
 * runs before every page load in this context/page instead. City/locality
 * are in localStorage (frontend/customer-web/src/lib/location.ts) and are
 * seeded the same way purely to skip the picker UI, not because it's
 * required for auth.
 */
export async function authenticateAsSeededCustomer(page: Page, fixture: CatalogFixture): Promise<void> {
  const expiresAt = new Date(Date.now() + 60 * 60 * 1000).toISOString();
  await page.addInitScript(
    ({ token, expiresAt, city, locality }) => {
      sessionStorage.setItem("nestly.accessToken", token);
      sessionStorage.setItem("nestly.accessTokenExpiresAt", expiresAt);
      localStorage.setItem("nestly.city", JSON.stringify(city));
      localStorage.setItem("nestly.locality", JSON.stringify(locality));
    },
    {
      token: fixture.customerAccessToken,
      expiresAt,
      city: { id: fixture.cityId, name: fixture.cityName, stateName: "E2E State" },
      locality: { id: fixture.localityId, name: fixture.localityName, pincodeId: fixture.pincodeId },
    }
  );
}

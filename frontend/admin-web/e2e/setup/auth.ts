import fs from "node:fs";
import path from "node:path";
import type { Page } from "@playwright/test";
import type { AdminFixture } from "./seed-admin";

export function loadFixture(): AdminFixture {
  return JSON.parse(fs.readFileSync(path.join(__dirname, "fixture.json"), "utf-8"));
}

/**
 * Pre-authenticates the admin session before any navigation, for specs
 * whose focus is something other than the sign-in flow itself (001-login
 * covers that separately). Tokens live in sessionStorage
 * (frontend/admin-web/src/lib/auth.ts) so Playwright's storageState
 * mechanism (localStorage/cookies only) can't carry them - addInitScript
 * runs before every page load in this context/page instead. Mirrors
 * frontend/customer-web/e2e/setup/auth.ts's authenticateAsSeededCustomer.
 */
export async function authenticateAsSeededAdmin(page: Page, fixture: AdminFixture): Promise<void> {
  await page.addInitScript(
    ({ token, refreshToken, expiresAt }) => {
      sessionStorage.setItem("nestly.admin.accessToken", token);
      sessionStorage.setItem("nestly.admin.refreshToken", refreshToken);
      sessionStorage.setItem("nestly.admin.accessTokenExpiresAt", expiresAt);
    },
    {
      token: fixture.adminAccessToken,
      refreshToken: fixture.adminRefreshToken,
      expiresAt: fixture.adminAccessTokenExpiresAtUtc,
    }
  );
}

import fs from "node:fs";
import path from "node:path";
import type { Page } from "@playwright/test";
import type { ProviderFixture } from "./seed-provider-job";

export function loadFixture(): ProviderFixture {
  return JSON.parse(fs.readFileSync(path.join(__dirname, "fixture.json"), "utf-8"));
}

/**
 * Pre-authenticates the provider session before any navigation, for specs
 * whose focus is something other than signing in.
 *
 * Tokens live in sessionStorage (`frontend/provider-web/src/lib/auth.ts`,
 * keys namespaced `nestly.provider.*`) so Playwright's storageState
 * mechanism (localStorage/cookies only) can't carry them - `addInitScript`
 * runs before every page load in this context/page instead. Same approach as
 * customer-web's `authenticateAsSeededCustomer` and admin-web's
 * `authenticateAsSeededAdmin`.
 *
 * Unlike admin-web, no spec here drives the real login form: provider sign-in
 * is OTP-only for a mobile number, and `SandboxNotificationProvider` never
 * exposes the generated code, so there is nothing for a browser to type. The
 * session this seeds is nonetheless a real one, minted by provider-api's own
 * `IProviderLoginService` (see `loginAsSeededProvider`).
 */
export async function authenticateAsSeededProvider(page: Page, fixture: ProviderFixture): Promise<void> {
  await page.addInitScript(
    ({ token, refreshToken, expiresAt }) => {
      sessionStorage.setItem("nestly.provider.accessToken", token);
      sessionStorage.setItem("nestly.provider.refreshToken", refreshToken);
      sessionStorage.setItem("nestly.provider.accessTokenExpiresAt", expiresAt);
    },
    {
      token: fixture.providerAccessToken,
      refreshToken: fixture.providerRefreshToken,
      expiresAt: fixture.providerAccessTokenExpiresAtUtc,
    },
  );
}

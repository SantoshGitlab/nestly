/**
 * Lighthouse CI `puppeteerScript` for task #350's mobile perf-budget job.
 *
 * The checkout page (`/booking/summary`) is wrapped in `RequireAuth`
 * (see `src/components/RequireAuth.tsx`), which reads a session from
 * `sessionStorage` (see `src/lib/auth.ts`) and client-redirects to `/login`
 * when it's missing. A plain Lighthouse navigation never has that session,
 * so without this script the checkout audit would silently measure the
 * login-redirect skeleton instead of the real checkout page.
 *
 * This script runs once per audited URL, before Lighthouse's own
 * navigation: it opens the app's origin, seeds the same three
 * `sessionStorage` keys `storeSession()` would set for a real login, using
 * the real access token the CI job's seed step obtained from consumer-api's
 * own `POST /auth/login/password` (see `e2e/setup/seed-catalog.ts`'s
 * `customerLogin()` — not a fabricated or bypassed token). Lighthouse then
 * navigates the target URL in the same page/session.
 *
 * Harmless no-op for the home and service-detail URLs, which don't require
 * auth — the same script is shared across all three audited URLs for
 * simplicity rather than branching per URL.
 */
module.exports = async (page, context) => {
  const targetUrl = new URL(context.url);

  const token = process.env.LHCI_CUSTOMER_TOKEN;
  const expiresAt = process.env.LHCI_CUSTOMER_TOKEN_EXPIRES;

  if (!token || !expiresAt) {
    // No seeded session available (e.g. a local dry run against mocked/empty
    // data) — leave sessionStorage untouched rather than failing the whole
    // collect run. The checkout URL will then render its real "redirecting
    // to sign in" skeleton, which is still a valid (if less meaningful)
    // audit target.
    return;
  }

  await page.goto(`${targetUrl.origin}/`, { waitUntil: "networkidle0" });

  await page.evaluate(
    ({ accessToken, refreshToken, tokenExpiresAt }) => {
      sessionStorage.setItem("nestly.accessToken", accessToken);
      sessionStorage.setItem("nestly.refreshToken", refreshToken);
      sessionStorage.setItem("nestly.accessTokenExpiresAt", tokenExpiresAt);
    },
    {
      accessToken: token,
      // The refresh token itself is never exercised by a Lighthouse audit
      // (no request runs long enough to need a refresh) — only its presence
      // matters, since lib/auth.ts's isAuthenticated() only reads the
      // access token + expiry.
      refreshToken: process.env.LHCI_CUSTOMER_REFRESH_TOKEN ?? "lhci-unused-refresh-token",
      tokenExpiresAt: expiresAt,
    },
  );
};

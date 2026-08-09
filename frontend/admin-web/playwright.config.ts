import { defineConfig, devices } from "@playwright/test";

/**
 * E2E suite (task 317, UAT-REPORT.md gap #1): real browser tests driving
 * admin-web against a real admin-api (:5177) and a real Postgres/Redis, per
 * docs/TESTING.md END-TO-END TESTING ("critical business journeys rather
 * than exhaustive UI coverage"). Mirrors
 * frontend/customer-web/playwright.config.ts exactly (same runner, same
 * conventions) - see e2e/README.md for how to start the stack.
 * `globalSetup` only seeds test data through admin-api, it does not start
 * docker-compose/dotnet/next.
 */
export default defineConfig({
  testDir: "./e2e",
  globalSetup: "./e2e/setup/global-setup.ts",
  fullyParallel: false,
  retries: 0,
  workers: 1,
  reporter: [["list"]],
  use: {
    baseURL: process.env.ADMIN_WEB_URL ?? "http://localhost:3001",
    trace: "retain-on-failure",
    screenshot: "only-on-failure",
  },
  projects: [
    {
      name: "chromium",
      use: { ...devices["Desktop Chrome"] },
    },
  ],
});

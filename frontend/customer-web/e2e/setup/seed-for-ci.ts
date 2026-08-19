/**
 * Standalone CI entrypoint for the mobile perf-budget job (task #350,
 * `.github/workflows/ci.yml`'s `lighthouse-mobile` job).
 *
 * Reuses `seedCatalog()` verbatim — the same function Playwright's
 * `globalSetup` (see `./global-setup.ts`) calls before the 140a-140d E2E
 * specs — rather than duplicating any of that seeding logic. This exists
 * only because the perf-budget job needs the resulting fixture (a real
 * service slug + an authenticated customer token + a saved address)
 * *without* also booting the full Playwright test runner and its specs,
 * which would pull in unrelated E2E assertions and a running customer-web
 * dev server as prerequisites the perf job doesn't have yet at this point
 * in its pipeline (see the workflow job's step ordering).
 *
 * Usage: npx tsx e2e/setup/seed-for-ci.ts
 * Requires: admin-api and consumer-api already reachable (ADMIN_API_URL /
 * CONSUMER_API_URL env vars, same defaults as seed-catalog.ts).
 *
 * Writes the same e2e/setup/fixture.json Playwright's globalSetup writes
 * (gitignored — see .gitignore), so the workflow can read
 * `serviceSlug` / `customerAccessToken` / `addressId` back out of it.
 */
import fs from "node:fs";
import path from "node:path";
import { seedCatalog } from "./seed-catalog";

const FIXTURE_PATH = path.join(__dirname, "fixture.json");

seedCatalog()
  .then((fixture) => {
    fs.writeFileSync(FIXTURE_PATH, JSON.stringify(fixture, null, 2));
    console.log(`Seeded catalog fixture for Lighthouse budget job: service slug "${fixture.serviceSlug}"`);
  })
  .catch((err) => {
    console.error("Catalog seeding failed:", err);
    process.exit(1);
  });

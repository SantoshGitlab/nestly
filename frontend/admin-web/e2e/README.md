# E2E suite (task 317)

Real browser tests (Playwright/Chromium) driving admin-web against a real
admin-api, Postgres and Redis - no mocking. Closes UAT-REPORT.md gap #1:
admin acceptance had only ever been verified at the API layer admin-web
itself calls, never through the admin UI. Mirrors
`frontend/customer-web/e2e/` exactly (same runner, same conventions, same
global-setup/fixture pattern) - see that suite's README for the reasoning
behind each choice; this one only repeats what's admin-web-specific.

Covers: sign-in (317a, including a rejected-password case), the dashboard
and sidebar navigation (317b), booking search -> detail (317c), and
admin-user search -> detail -> deactivate/reactivate (317d, the one full
write flow). Deliberately not attempting all 32 admin-api controllers'
worth of UI in one pass - a solid foundation others can extend beats a
sprawling first cut.

## Prerequisites (start once, outside Playwright)

```bash
docker compose up -d postgres redis
bash database/scripts/apply-migrations.sh
docker exec -i nestly-postgres-1 psql -U nestly -d nestly < database/seed/dev-admin-seed.sql

ASPNETCORE_ENVIRONMENT=Development dotnet run --project backend/admin-api/AdminApi --urls http://localhost:5177 &

cd frontend/admin-web
npm run dev &                # http://localhost:3001 (package.json's "dev" pins -p 3001)
```

`ASPNETCORE_ENVIRONMENT=Development` matters for the same two reasons as
customer-web's suite: relaxed `RateLimiting` overrides (login is rate
limited in production) and the CORS origin (`http://localhost:3001`) that
lets a real browser call admin-api at all - see
`frontend/customer-web/e2e/README.md` for the full explanation and the
`AddNestlyCors` reference.

consumer-api is not required - this suite only exercises admin-web and
admin-api. The bookings-list/detail spec (317c) does need at least one real
booking to already exist in the database; if this is a fresh environment
with no bookings yet, run `frontend/customer-web/e2e`'s suite first (its
140b spec creates one), or create one manually through admin-web/customer-web.

## Run

```bash
cd frontend/admin-web
npm install                        # first time only - pulls in @playwright/test
npx playwright install chromium    # first time only - browser binary
npx playwright test                # all specs, headless
npx playwright test 317a-login.spec.ts  # one file
npx playwright test --headed --debug    # interactive
```

(or `npm run test:e2e` for the equivalent of the plain `npx playwright test`.)

## How data is set up

`playwright.config.ts`'s `globalSetup` (`e2e/setup/global-setup.ts`) calls
`e2e/setup/seed-admin.ts`, which:

- logs in as the seeded `dev-admin@nestly.local` Super Admin (bootstrapped by
  `database/seed/dev-admin-seed.sql` - see that file's header comment for why
  this one account is a direct-DB seed rather than created through an API:
  there is no admin self-registration endpoint by design),
- finds-or-creates a dedicated `e2e-admin-user@nestly.local` account through
  the real admin-api (`POST /api/v1/admin/admin-users`) for the
  list/detail/lifecycle specs to exercise, and (re)activates it every run so
  317d always starts from a known state,
- reads one existing booking via `GET /api/v1/admin/bookings` for 317c's
  search -> detail spec.

The result is written to `e2e/setup/fixture.json` (gitignored, regenerated
every run) for specs to read via `loadFixture()`. This is the same shape as
customer-web's suite: everything that has an API path goes through the real
API, not direct SQL.

## Auth

`e2e/setup/auth.ts`'s `authenticateAsSeededAdmin` pre-seeds the admin
session into `sessionStorage` via `addInitScript` before any spec navigates
- admin-web's tokens live in sessionStorage
(`frontend/admin-web/src/lib/auth.ts`, keys namespaced `nestly.admin.*`), not
cookies, so Playwright's `storageState` mechanism can't carry them; same
approach as customer-web's `authenticateAsSeededCustomer`.

317a-login.spec.ts is the exception: it drives the real login form instead,
because that is the flow under test. Admin sign-in needed no dev-only
test-auth bypass route (unlike provider-web's mobile-OTP login) - it is a
plain email/password form with seeded credentials, directly testable through
the UI.

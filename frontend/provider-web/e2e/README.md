# E2E suite (task 385)

Real browser tests (Playwright/Chromium) driving provider-web against a real
provider-api, admin-api, consumer-api, Postgres and Redis - no mocking.
Closes PRODUCTION-READINESS.md §4.1's provider gap: provider-web owns the
whole job lifecycle and had zero specs, which is why the 2026-08-18 QA sweep
listed that lifecycle as unwalked past "Assigned".

Mirrors `frontend/customer-web/e2e/` and `frontend/admin-web/e2e/` exactly
(same runner, same conventions, same global-setup/fixture pattern) - see
customer-web's README for the reasoning behind each choice; this one only
repeats what's provider-web-specific.

Covers: the auth guard, the job list and its status/date filters (385a), and
the full lifecycle **Accept -> En route -> Arrived -> In progress ->
Completed**, including the photo + checklist completion verification that
gates completion (385b).

## Prerequisites (start once, outside Playwright)

```bash
docker compose up -d postgres redis
bash database/scripts/apply-migrations.sh
docker exec -i nestly-postgres-1 psql -U nestly -d nestly < database/seed/dev-admin-seed.sql
docker exec -i nestly-postgres-1 psql -U nestly -d nestly < database/seed/dev-customer-seed.sql
docker exec -i nestly-postgres-1 psql -U nestly -d nestly < database/seed/dev-provider-seed.sql

ASPNETCORE_ENVIRONMENT=Development dotnet run --project backend/consumer-api/ConsumerApi --urls http://localhost:5257 &
ASPNETCORE_ENVIRONMENT=Development dotnet run --project backend/admin-api/AdminApi   --urls http://localhost:5177 &
ASPNETCORE_ENVIRONMENT=Development dotnet run --project backend/provider-api/ProviderApi --urls http://localhost:5337 &

cd frontend/provider-web
npm install                        # first time only - pulls in @playwright/test
npx playwright install chromium    # first time only - browser binary
npm run dev &                      # http://localhost:3002 (package.json's "dev" pins -p 3002)
```

All three APIs are needed, unlike the other two suites: admin-api seeds the
catalog and performs the assignment, consumer-api creates and pays for the
booking, and provider-api is what provider-web itself talks to.

`ASPNETCORE_ENVIRONMENT=Development` matters for three reasons here (the
other two suites' README covers the first two): the relaxed `RateLimiting`
overrides, the CORS origin (`http://localhost:3002` - see provider-api's
`appsettings.Development.json`), and the dev-only login endpoint this suite
authenticates through, which is only mapped in Development at all.

## Run

```bash
cd frontend/provider-web
npm run test:e2e                        # all specs, headless
npx playwright test 385b-job-lifecycle.spec.ts  # one file
npx playwright test --headed --debug    # interactive
```

## How data is set up

`playwright.config.ts`'s `globalSetup` (`e2e/setup/global-setup.ts`) calls
`e2e/setup/seed-provider-job.ts`, which:

- reuses **customer-web's `seedCatalog()` verbatim** (imported across app
  folders - it is a dependency-free module) to build the geography ->
  category -> service -> serviceability -> slot-window chain through
  admin-api. There is deliberately no second copy of that logic here: the
  same function already backs customer-web's suite and CI's
  `lighthouse-mobile` job, and a divergent copy is how those three drift
  apart.
- mints a real provider session through provider-api's dev-only
  `POST /api/v1/auth/dev/login-as-provider` (docs/DEVOPS.md "Dev-only
  provider test login"), the same backdoor provider-web's own "Test login"
  button proxies. **Why a backdoor rather than the login form:** provider
  sign-in is OTP-only, and `SandboxNotificationProvider` never exposes the
  generated code - there is literally nothing for a browser to type. The
  session it returns is a real one, minted by `IProviderLoginService`.
- creates a booking, pays for it through the sandbox gateway, releases it to
  `AwaitingFulfilment` and assigns the seeded provider to it - each through
  the real endpoint the corresponding UI calls, so the specs start from a job
  in exactly the state a provider finds one in.

The result is written to `e2e/setup/fixture.json` (gitignored, regenerated
every run) for specs to read via `loadFixture()`.

### Why the slot date is chosen, not fixed

A Completed job's assignment row stays `Accepted`, and
`ProviderScheduleConflictService` counts that as a live commitment. So the
second run of this suite on a fixed date would fail with a 409
`ProviderDoubleBooked` - not on a real defect. The seed reads the provider's
own job list first and books the first upcoming day it has nothing live on.

## Auth

`e2e/setup/auth.ts`'s `authenticateAsSeededProvider` pre-seeds the session
into `sessionStorage` via `addInitScript` before any spec navigates -
provider-web's tokens live in sessionStorage
(`src/lib/auth.ts`, keys namespaced `nestly.provider.*`), not cookies, so
Playwright's `storageState` mechanism can't carry them. Same approach as
customer-web's `authenticateAsSeededCustomer` and admin-web's
`authenticateAsSeededAdmin`.

## Locator gotchas this app has

- **Navigation renders twice.** `ProviderSidebar` (a side rail from `md` up)
  and `ProviderTabBar` (a bottom tab bar below it) are both always in the
  DOM, hidden by CSS at their respective breakpoints - so
  `getByRole("link", { name: "Jobs" })` matches two elements. These specs
  navigate with `page.goto` rather than by clicking nav.
- **Status labels render in several places at once.** The same
  `JobStatusBadge` text appears on every list card, in the detail heading,
  and as an `<option>` in the list's status filter. 385b scopes its badge
  assertions to the `<header>` containing the page's `<h1>`.

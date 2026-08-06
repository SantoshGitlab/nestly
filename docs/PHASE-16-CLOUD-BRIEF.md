# Phase 16 — cloud agent brief (end-to-end order tracking)

You are continuing an in-progress feature. Work autonomously and finish as many
tasks as you can. This file is your complete instruction set.

## Step 0 — setup (stop and report if any step fails)

1. `git checkout feature/phase-16-order-tracking && git pull`.
   **All the work is on this branch, not `main`.** If the branch is missing, stop
   and say so.
2. Toolchain: `dotnet --version` (needs the .NET 8 SDK) and `node --version`.
   If the .NET SDK is absent you cannot verify backend work — stop and report
   that rather than writing code you cannot verify.
3. Baseline: `dotnet build Nestly.sln -v q --nologo`, then
   `dotnet test Nestly.sln --nologo -v q`.
   Expected: build clean, **1323 passing** (Catalog 1075 + Identity 236 +
   CustomerManagement 12) plus Performance.Tests 8. If the baseline is already
   red, report it and stop — do not build on a broken tree.
4. Read `AGENTS.md`, `docs/CODING-STANDARDS.md`, `docs/DOTNET.md` and
   `docs/PROVIDER.md`. Match the house style exactly; this codebase is very
   consistent and consistency is valued over your own preferences.

## What the feature is

Phase 16 is an end-to-end order tracking system: the customer watches their
booking live — status timeline, provider en-route/arrived, live location on a
Google Map, a real ETA — and Google Maps road travel time drives correct
provider-to-customer assignment.

Two requirements stated explicitly by the user, which override any instinct to
simplify: **tracking must be real, not simulated**, and **Google Maps is used
for proper provider assignment**.

## Already done on this branch — do not redo

| Task | What landed |
|---|---|
| 264 | `BookingStatus.ProviderEnRoute` / `ProviderArrived` + lifecycle transitions + labels/buckets; fixed a pre-existing `Expired` mapper defect |
| 265 | Haversine extracted to `Nestly.BuildingBlocks.Geo.GeoDistance` |
| 266 | `IRouteEstimateProvider` — Google **Routes API** (`computeRouteMatrix`) + sandbox fallback, in `backend/shared/Application/Routing/` |
| 267 | Auto-assignment ranks candidates by real road travel time, kill switch `AutoAssignmentOptions.RouteRankingEnabled` |
| 288 | Provider double-booking prevention — `IProviderScheduleConflictService`, serializable transaction, Postgres `EXCLUDE USING gist` backstop |

Read each row's `notes` column in `tasks.csv` before touching adjacent code —
the notes record decisions and deliberate deviations you must not undo. Two that
bite most often:

- The `BookingStatus` enum **crosses the wire as its ordinal** and all three
  frontends mirror it by hand. Never insert a value mid-enum; always append.
- Task 266 deliberately uses the **Routes API, not Distance Matrix** — Distance
  Matrix went legacy on 2025-03-01 and cannot be enabled on a new Cloud project.
  Do not "fix" this back.

## Your job

Work the remaining Phase 16 rows in `tasks.csv` — **268–287, 289, 290** — which
are `status=todo`. Each row's `task` column is a full specification; the `notes`
column records dependencies as `depends on #N`.

1. `python3 scripts/task_claim.py status` shows which rows are free (it already
   understands the `depends on #N` notes and hides blocked rows).
2. Claim with
   `python3 scripts/task_claim.py claim <id> --owner cloud-agent --pid $$`.
3. Implement it. Respect dependency order; prefer the lowest free id.
4. Verify — **every task, no exceptions**: `dotnet build Nestly.sln -v q --nologo`
   and `dotnet test Nestly.sln --nologo -v q` must both be clean, and the pass
   count must not go down. For any frontend you touch, run `npx tsc --noEmit`
   **and** `npm run build` in that app's directory
   (`frontend/customer-web`, `frontend/provider-web`, `frontend/admin-web`).
5. Close it with
   `python3 scripts/task_claim.py done <id> --note "DONE <date> by cloud-agent. ..."`.
   Never hand-edit `tasks.csv`. The note is the project's real record — make it
   substantive: what changed and where, decisions and their justification, what
   you found but deliberately did not fix, tests added, final pass count.
6. `git commit` after each completed task and `git push` to the same branch.
   Do not open a PR; do not merge to `main`.

## House conventions that matter here

- A fix ships with a test **proven to catch the defect**: temporarily restore
  the broken behaviour, watch the test fail, restore the fix. Say so in the note.
- Never make a real network call in a test. `GoogleMapsRouteEstimateProvider`
  uses a **named** `HttpClient` via `IHttpClientFactory` precisely so its
  `HttpMessageHandler` can be stubbed (that is what task 286 is for).
- The runtime database is PostgreSQL; the test suite runs on in-memory SQLite
  (`TestDatabase`, which uses `EnsureCreated` and therefore never runs
  migrations). Where the two diverge, **document the divergence, do not hide
  it** — see the notes on tasks 252 and 288 for the precedent.
- Secrets: no API key in any committed file, log, exception message or cache key.
- If a task turns out to be bigger than its row, do the part that is genuinely
  in scope and **add a new row** for the remainder rather than silently
  expanding. Tasks 289 and 290 were created exactly that way.

## If you get blocked

Do not guess at a product decision. Finish everything that is not blocked, then
report clearly: which tasks completed, which are blocked and on what, and what
you would need in order to proceed. A short honest report beats a long
speculative one.

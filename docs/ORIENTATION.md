# ORIENTATION.md

**Start here.** This document explains what Nestly is, how the pieces fit
together, what actually exists today versus what is still planned, and the
non-obvious rules that will bite you if you do not know them.

The rest of the documentation suite describes how things *should* be built
(see [README.md](README.md) for the index and topic ownership). This document
is the only one that describes **the current state of the repository** — so
treat the others as the specification and this one as the map.

Last verified: **2026-08-17**, against `main` at commit `e020819`. The
2026-08-08 pass against `39cc78a` remains the last one that **built and tested**
the solution; this pass re-counted the backlog and the code surface statically
and corrected the figures below. Nothing was rebuilt or re-run.

The backlog is **effectively complete**: `tasks.csv` carries 647 rows, of which
596 are `done`, 50 are `decomposed` parent placeholders already satisfied by
their own lettered subtasks, and **one is `todo`**. All nineteen phase groups
(0–18, plus a small set of unphased early setup rows) are closed except that
one row. See TASKS-SUMMARY.md for the per-phase table.

The single open row is **task 318** — execute QA phases 3 and 4 from
[QA-REPORT-2026-08-07.md](QA-REPORT-2026-08-07.md). That report's verdict is
**NO-GO for release on absence of evidence**: 587 inventoried UI features are
runtime-unverified and cross-service booking consistency across the three
backends is unmeasured. Backlog completeness is therefore not the same as
release readiness, and this row is the gap between them.

Work beyond the backlog is tracked in
**[ENHANCEMENT-BACKLOG-2026-08-08.md](ENHANCEMENT-BACKLOG-2026-08-08.md)** —
verified gaps between what the specs describe and what the code does, with
file:line evidence. That document, not this one, is where the next task comes
from.

---

## 1. WHAT NESTLY IS

An enterprise home-services marketplace. Customers discover, book, pay for and
review at-home services; internal teams run the full commerce lifecycle through
a separate admin panel.

It is a **modular monolith** — one deployable per API surface, with strict
internal module boundaries so business modules can later be extracted into
services without a rewrite. It is not microservices, and should not be built
as if it were.

Business context and module inventory: [PROJECT.md](PROJECT.md).
Full requirements: [SRS.md](SRS.md).

---

## 2. WHERE THE PROJECT ACTUALLY STANDS

This is the section most likely to be out of date, and the most important to
keep honest.

**Every phase in `tasks.csv` is closed but one row.** The platform is
feature-complete against its own backlog: three APIs, three frontends, 93
domain entity classes (across 145 files in `shared/Domain`) and 76 migrations.
What remains is not backlog delivery but the gap set in
[ENHANCEMENT-BACKLOG-2026-08-08.md](ENHANCEMENT-BACKLOG-2026-08-08.md) and the
release-readiness work in [QA-REPORT-2026-08-07.md](QA-REPORT-2026-08-07.md).

### What genuinely exists and is verified

Verified 2026-08-08 by building and testing a clean checkout of `origin/main`
(not a working tree): `dotnet build Nestly.sln` **0 errors / 0 warnings**,
`dotnet test Nestly.sln` **1767 passing / 0 failing**.

| Area | State |
|---|---|
| Solution & layering | 18 projects across 3 API hosts + 4 shared libraries + 4 test projects; dependencies flow inward |
| Backend APIs | `consumer-api` (27 controllers / 85 endpoints), `admin-api` (37 / 289), `provider-api` (9 / 49) |
| Frontends | `customer-web`, `admin-web`, `provider-web` — all three are real product apps, not scaffolds |
| Persistence | EF Core + PostgreSQL, snake_case, configuration-by-assembly-scan, 76 migrations, 94 entity configurations, 72 `DbSet`s, replayable from an empty database (task 207) |
| Domain | 93 entity classes; 15 derive from `AggregateRoot` and raise domain events |
| Modules live | Identity, catalog, serviceability, slots, booking, payments, wallet, coupons, cancellation/reschedule, reviews, support, notifications, admin panel + RBAC, provider/partner, referral, Nestly Coins, subscriptions, recurring bookings, chat, live tracking, completion verification |
| Tests | 1767 across `Catalog.Tests` (1443), `Identity.Tests` (304), `CustomerManagement.Tests` (12), `Performance.Tests` (8) — SQLite-backed integration, not just unit |
| Caching | `ICacheService` over Redis with in-process fallback — in real use (catalog, notification templates) |
| Background jobs | Hangfire on PostgreSQL — in real use (expiry sweeps, recurring-booking scheduling, notification intents) |
| Realtime | SignalR — chat and live booking tracking |
| Audit trail | `audit_log` + `IAuditLogWriter`, enlisted in the caller's unit of work |
| Observability | Serilog structured logging, correlation ids, `IMetricsService` |
| DevOps | Dockerfiles, docker-compose (Postgres + Redis + APIs), GitHub Actions CI |

### What does **not** exist yet

Be blunt about this, because feature-complete-against-backlog is not the same
as complete:

- **The gaps in
  [ENHANCEMENT-BACKLOG-2026-08-08.md](ENHANCEMENT-BACKLOG-2026-08-08.md)** —
  including several features whose backend ships and whose frontend never calls
  it (referral attribution at sign-up, admin account unlock, provider push
  registration), and one, wallet-credit-at-checkout, where the product tells the
  customer something the code does not do.
- **The MediatR request pipeline (`ICommand`/`IQuery`) still has zero
  implementations** — `Application/Abstractions/Messaging.cs` defines the
  abstractions and nothing implements them; every controller calls its service
  directly rather than through `ISender`. Grep before assuming a command or
  query handler exists.
- **Domain events, by contrast, are genuinely live** — this reversed since the
  2026-08-01 verification. `DomainEventDispatchInterceptor` publishes them
  through MediatR's `IPublisher` on `SaveChanges`, and 16 handlers consume 8
  event types (notifications, metrics, realtime broadcast, referral fraud
  signals). Note the ordering consequence: handlers run **after** the save,
  inside the same request.
- **Release readiness.** QA's 2026-08-07 verdict is **NO-GO**, on absence of
  evidence rather than known defects: all 587 inventoried UI features remain
  runtime-unverified, and there is no admin-web browser E2E suite.

---

## 3. ARCHITECTURE IN ONE PASS

Clean Architecture. **Dependencies point inward, always.**

```
        ConsumerApi        AdminApi          <- hosts, composition roots
             \               /
              \             /
               Infrastructure                <- EF Core, Redis, Hangfire, HTTP
                     |
                Application                  <- use cases, abstractions, MediatR
                     |
                  Domain                     <- entities, business rules
                     |
              BuildingBlocks                 <- primitives shared by all layers
```

- **Domain** — entities and invariants. No framework dependencies.
- **Application** — orchestration, plus the *abstractions* Infrastructure
  implements (`ICacheService`, `IAuditLogWriter`, `IAuditContextProvider`).
  Framework-free.
- **Infrastructure** — the outermost layer and the only one that knows about
  EF Core, Redis, Hangfire and ASP.NET Core. It has a
  `FrameworkReference` to `Microsoft.AspNetCore.App` deliberately.
- **APIs** — thin. `Program.cs` composes layers and configures the pipeline.

The rule that keeps this honest: **when Infrastructure needs to hand something
to business code, define the interface in Application and implement it in
Infrastructure.** Never let Application reference a framework type.

Detail: [ARCHITECTURE.md](ARCHITECTURE.md).

---

## 4. REPOSITORY MAP

```
backend/
  consumer-api/ConsumerApi/     Customer-facing API. Enqueues jobs, runs none.
  admin-api/AdminApi/           Admin API. Runs the Hangfire server + dashboard.
  provider-api/ProviderApi/     Service-provider API (jobs, availability, earnings)
  shared/
    Domain/                     Entities and business rules
    Application/                Use cases + abstractions Infrastructure implements
    Infrastructure/             Persistence, caching, jobs, auditing, realtime
    BuildingBlocks/             Result/Error, entity primitives, middleware
  tests/                        Catalog, Identity, CustomerManagement, Performance
database/
  migrations/                   EF Core migrations AND the model snapshot (see §5)
  scripts/  seed/               Operational SQL, idempotent seed data
frontend/
  customer-web/                 Next.js — customer app
  admin-web/                    Next.js — admin panel
  provider-web/                 Next.js — provider app
docs/                           Documentation suite — README.md is the index
tasks.csv                       The backlog: 627 rows, all closed (see §8)
```

---

## 5. NON-OBVIOUS RULES

These are the things that have already cost real debugging time. Read them
before touching the relevant area.

### Migrations live outside the project that compiles them

`database/migrations/` sits outside `Infrastructure/`, so the SDK's implicit
glob does not pick it up. `Infrastructure.csproj` includes it explicitly:

```xml
<Compile Include="..\..\..\database\migrations\**\*.cs" LinkBase="Migrations" />
```

**Why this matters:** without that line, `dotnet build` still succeeds — the
migration files simply are not part of any project — while
`dotnet ef migrations list` reports *no migrations*. A green build is not
evidence that migrations exist.

Also: `dotnet ef migrations add` writes the migration to `-o` but writes
`NestlyDbContextModelSnapshot.cs` to the project's **default** `Migrations/`
folder. Both then compile and the build breaks on duplicate types. After adding
a migration, move the snapshot into `database/migrations/` and delete the
generated folder.

### Entity configuration is discovered, never registered

`NestlyDbContext.OnModelCreating` calls `ApplyConfigurationsFromAssembly`. Add
an `IEntityTypeConfiguration<T>` under
`Infrastructure/Persistence/Configurations/` and it is picked up. **Never add
manual `modelBuilder.Entity<T>()` registrations** — they duplicate the scan.

### The database is snake_case

`UseSnakeCaseNamingConvention()` maps `OccurredOnUtc` → `occurred_on_utc`. Any
raw SQL — index filters, check constraints, `HasFilter(...)` — must be written
in **snake_case**, because it bypasses the convention. This class of bug
survives `dotnet build` and only appears when the migration hits Postgres.

### Two different things are called "auditing"

- **Column stamping** — `IAuditable` + `AuditableEntityInterceptor` fill
  `CreatedOnUtc`/`ModifiedOnUtc`. Records *when a row changed*.
- **The audit trail** — the `audit_log` table records *who did what to which
  entity, from where*.

`IAuditLogWriter` **enlists in the caller's unit of work and does not save** —
your `SaveChangesAsync` commits the audit row in the same transaction as the
change it describes. A rolled-back operation must not leave a phantom entry.

### The cache is advisory

`ICacheService` never throws for transport failures — an unreachable Redis
degrades to the source of truth and logs a warning. Build keys through
`CacheKeys`; inlining key strings is how the writer and the invalidator drift
apart. `GetOrCreateAsync` is cache-aside, **not a lock**: concurrent misses may
each run the factory, so never give it a factory with side effects.

### Background jobs are split across processes

The admin API runs the Hangfire server and the dashboard; the consumer API only
enqueues; tests do neither (`BackgroundJobs:ServerEnabled`). Retries re-run the
whole method, so **every job must be idempotent** and must honour its
`CancellationToken`.

### Security rules that are absolute

Never log passwords, OTPs, tokens or PII. Always hash before storing (OTP codes
are SHA-256 hashed, never plaintext). Never hardcode credentials. See
[SECURITY.md](SECURITY.md).

---

## 6. RUNNING AND VERIFYING

```bash
docker compose up -d postgres redis
```

```bash
dotnet build Nestly.sln
```

```bash
dotnet ef database update --project backend/shared/Infrastructure --startup-project backend/consumer-api/ConsumerApi
```

Health checks once an API is running: `/health/live` (process up) and
`/health/ready` (Postgres and Redis reachable).

**A passing build is not proof of work.** This repository has been burned by
that assumption repeatedly (§7). For anything touching persistence, caching or
jobs, verify against real infrastructure: apply migrations to a throwaway
Postgres and inspect the result, exercise the code through the real DI
container, and confirm the dependency actually served the traffic.

---

## 7. HISTORY YOU NEED IN ORDER TO TRUST THE REPO

Large parts of this backlog were attempted by automated local-model workers.
That history left artefacts you will encounter:

- **~60 tasks were once marked done that were never implemented.** An early
  worker used `npm run build` as its verification command — in a .NET repo with
  no root `package.json`. It could never pass, so verification silently
  degraded to "the model says it is done." Those tasks were audited and reset;
  their `tasks.csv` notes still record the audit findings.
- **Fabricated code has appeared more than once** — SQL Server APIs in this
  PostgreSQL project, invented method names, namespaces derived from folder
  paths instead of `RootNamespace`. It was discarded, not patched.
- **`_salvage/` and `tasks-corrupted.csv` are gone.** Both are referenced by
  older documents and commit messages; neither exists on `main` any more.
  Nothing was promoted out of `_salvage/` unverified.
- **A defensive fix can itself be the outage.** `NotificationTemplateRepository`
  shipped a filter (088ba63) that stops one malformed `notification_template`
  row from killing all dispatch — written as PostgreSQL-only raw SQL, which the
  SQLite-backed suite could not execute, so it landed with a permanently
  failing test and no coverage of the behaviour it existed for. Fixed
  2026-08-08. The lesson generalises: **raw SQL must run on both providers, or
  the tests that would catch it silently stop running.**

The practical rule: **`tasks.csv` status is a claim; the code is the evidence.**
A task note saying "verified by claude-code" records what was actually checked
and how — prefer those. When in doubt, grep for the thing before assuming it
exists. The same applies to this document: it was materially wrong for a week
(claiming Phase 2 was active, with no booking or payments, while all eleven
phases were in fact closed) because nothing regenerates it. If you find it
stale, fix it in the same pass.

---

## 8. THE ROADMAP

`tasks.csv` carries a `phase` column; work proceeds phase by phase. Counts
below are from `tasks.csv` itself on 2026-08-08, not from TASKS-SUMMARY.md
(which is regenerated only at phase boundaries and lags).

| Phase | Scope | Done | Open |
|---|---|---|---|
| 0 | Foundation — solution, persistence, caching, jobs, audit, DevOps | 25/25 | — |
| 1 | Identity & Customer — registration, JWT, profile, addresses | 46/46 | — |
| 2 | Catalog & Serviceability | 69/72 | — |
| 3 | Booking Core | 47/56 | — |
| 4 | Payments & Financial | 40/46 | — |
| 5 | Post-Booking — reviews, support, notifications | 40/47 | — |
| 6 | Admin Panel | 104/121 | 2 |
| 7 | Partner — provider identity, onboarding, assignment, earnings (PROVIDER.md) | 23/27 | — |
| 8 | Hardening & Launch | 35/41 | — |
| 9 | Referral & Growth (REFERRAL.md) | 16/16 | — |
| 10 | Product Enhancements — subscriptions, recurring bookings, chat, completion verification (PRODUCT-ENHANCEMENTS.md) | 22/22 | — |
| 11 | Nestly Coins & Loyalty (NESTLY-COINS.md) | 5/5 | — |
| 12 | Premium UI & UX Overhaul | 21/21 | — |
| 13 | Booking Funnel Defects | 12/12 | — |
| 14 | Automatic Provider Assignment | 9/9 | — |
| 15 | QA Audit Defects | 13/13 | — |
| 16 | End-to-End Order Tracking (TRACKING.md) | 32/32 | — |
| 17 | Recurring Services | 5/5 | — |
| 18 | Spec-Gap Closure — verified gaps between the specs and the code | 2/20 | 16 |

The `Done` column counts only rows with status `done`; the remainder in
phases 2–8 are `decomposed` parent placeholders, each already satisfied by its
own lettered subtasks (e.g. `#35` → `#35a`..`#35d`). A `done/total` ratio is
only meaningful against a phase's *current* total, not the number originally
planned. **Provider/Partner moved from Phase 8 to Phase 7 on 2026-07-31** — see
PROVIDER.md's STATUS section for why.

**Phases 0–17 are closed.** The open work is **Phase 18**, derived from
[ENHANCEMENT-BACKLOG-2026-08-08.md](ENHANCEMENT-BACKLOG-2026-08-08.md), plus
two low-priority admin table-UX rows (`#301`, `#302`) in Phase 6.

Several sessions work this repository concurrently, and a `todo` row does not
mean nobody has started it. **Claim before you begin** —
`python3 scripts/task_claim.py status`, then
`task_claim.py next --owner <label> --pid <pid>` — and close rows with
`task_claim.py done <id> --note ...` rather than hand-editing `tasks.csv`.

---

## 9. WHERE TO GO NEXT

| You want to know about | Read |
|---|---|
| Which document owns a topic | [README.md](README.md) |
| Business domain and modules | [PROJECT.md](PROJECT.md) |
| Full requirements | [SRS.md](SRS.md) |
| Layer boundaries | [ARCHITECTURE.md](ARCHITECTURE.md) |
| .NET / ASP.NET conventions, caching and jobs usage | [DOTNET.md](DOTNET.md) |
| Schema, EF Core, indexing, auditing | [DATABASE.md](DATABASE.md) |
| REST conventions and versioning | [API.md](API.md) |
| Auth, secrets, security rules | [SECURITY.md](SECURITY.md) |
| Test strategy | [TESTING.md](TESTING.md) |
| Docker, CI/CD, operations | [DEVOPS.md](DEVOPS.md) |
| How AI agents must behave here | [../.claude/CLAUDE.md](../.claude/CLAUDE.md) |

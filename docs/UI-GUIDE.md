# Nestly — Illustrated Product UI Guide

## PURPOSE

A screenshot-based, UI-focused companion to [WORKFLOW.md](WORKFLOW.md)'s
Mermaid diagrams (task 207) — WORKFLOW.md answers "what happens when," this
document is meant to answer "what does that actually look like," plus give
a new engineer or reviewer the exact steps to get the three apps running
locally from a clean checkout.

This document is **not authoritative** on behavior — where it and SRS.md
disagree, SRS.md is correct and this file is stale.

## STATUS

**Setup/initialization instructions below are complete and verified against
the actual `docker-compose.yml`, migration scripts and seed scripts in this
repository** — not assumed from memory.

**The screenshot walkthrough itself is not yet captured.** Bringing up the
local stack during this pass surfaced that the shared local Postgres
volume's migration history (`__EFMigrationsHistory`) is out of sync with its
actual contents — applying this worktree's latest migrations
(`SeedReferralNotificationTemplates`) failed on a primary-key collision,
meaning those rows already exist under a state the tracked history doesn't
account for, most likely because a concurrent process was also working
against this same local database at the time. Screenshotting through a
database in an inconsistent, actively-changing state would produce
misleading images and risks masking or fabricating what "done" means here,
so this pass stops short of that rather than push through it. Section
["Screens to capture"](#screens-to-capture) below is a concrete, ready-to-run
checklist — completing it is a mechanical follow-up once the local stack is
confirmed stable (see [First-time setup](#first-time-setup)), not a
redesign.

## FIRST-TIME SETUP

Prerequisites: Docker, the .NET 8 SDK, Node.js (see each frontend's
`package.json` `engines` field for the exact version), and `dotnet-ef`
(`dotnet tool install -g dotnet-ef`).

1. **Start Postgres and Redis:**
   ```bash
   docker compose up -d postgres redis
   ```
2. **Apply migrations** (creates/updates the schema against the compose
   database):
   ```bash
   ./database/scripts/apply-migrations.sh
   ```
3. **Seed development accounts.** `AdminUser` and `CustomerAuthIdentity`
   have no self-registration path by design (see each seed script's header
   comment), so the first account of each is a one-time direct insert:
   ```bash
   psql "postgresql://nestly:nestly_dev@localhost:5432/nestly" -f database/seed/dev-admin-seed.sql
   psql "postgresql://nestly:nestly_dev@localhost:5432/nestly" -f database/seed/dev-customer-seed.sql
   ```
   Both scripts are idempotent (`WHERE NOT EXISTS ...` guards) - safe to
   re-run.
4. **Run the three backend APIs** - either via Docker:
   ```bash
   docker compose up -d consumer-api admin-api partner-api
   ```
   or directly for faster local iteration (each on its own default port):
   ```bash
   dotnet run --project backend/consumer-api/ConsumerApi   # http://localhost:5257
   dotnet run --project backend/admin-api/AdminApi         # http://localhost:5177
   dotnet run --project backend/partner-api/PartnerApi     # http://localhost:5337
   ```
5. **Run the three frontends** (each reads `NEXT_PUBLIC_API_URL`, defaulting
   to the ports above if unset):
   ```bash
   npm --prefix frontend/customer-web run dev   # http://localhost:3000
   npm --prefix frontend/admin-web run dev      # http://localhost:3001
   npm --prefix frontend/partner-web run dev    # http://localhost:3002
   ```
6. **Sign in.**

   | App | URL | Credentials |
   |---|---|---|
   | Customer web | `http://localhost:3000/login` → "Email & password" tab | `e2e-customer@nestly.local` / `E2eCustomer!Passw0rd` |
   | Admin web | `http://localhost:3001/login` | `dev-admin@nestly.local` / `E2eTest!Passw0rd` |
   | Partner web | `http://localhost:3002/login` | No seed exists (docs/DEVOPS.md/database/seed has no `dev-partner-seed.sql`) - register a new partner via `/register`, then sign in with a real mobile-OTP code. The OTP is never logged or exposed via any dev bypass (see `dev-customer-seed.sql`'s header comment on the equivalent customer case); read the code directly from the `partner_otp` table in the local dev database if a UI walkthrough needs one without a real SMS provider configured. |

   All three passwords above are seeded local-dev-only values, never valid
   outside a local/CI database - see each seed script's own warning.

   Since task 206, `http://localhost:3000/login` alone can also sign in as
   Admin or Partner via the account-type selector at the top of the page -
   no need to visit `:3001`/`:3002` directly except to exercise a direct
   bookmark to those origins.

## Screens to capture

Once the stack above is confirmed stable, capture one PNG per row below into
`docs/assets/ui-guide/<app>/<name>.png` and embed it under the matching
heading with `![<name>](assets/ui-guide/<app>/<name>.png)`. Keep the browser
at a consistent width (1440px suggested) so images stay visually consistent.

### Customer web (`docs/assets/ui-guide/customer-web/`)

| Screenshot | Route | Notes |
|---|---|---|
| `login` | `/login` | Show the account-type selector (task 206) |
| `home` | `/` | |
| `categories` | `/categories` | |
| `service-detail` | `/services/[slug]` | Any active service |
| `booking-summary` | `/booking/summary` | Mid-checkout, one service in cart |
| `booking-detail` | `/bookings/[id]` | A `Completed` booking, to show the status timeline |
| `wallet` | `/wallet` | |
| `profile` | `/profile` | |

### Admin web (`docs/assets/ui-guide/admin-web/`)

| Screenshot | Route | Notes |
|---|---|---|
| `login` | `/login` | |
| `dashboard` | `/dashboard` | |
| `bookings` | `/bookings` | |
| `booking-detail` | `/bookings/[bookingId]` | |
| `catalog` | `/catalog` | |
| `partners` | `/partners` | |
| `coupons` | `/coupons` | |
| `reports` | `/reports` | |

### Partner web (`docs/assets/ui-guide/partner-web/`)

| Screenshot | Route | Notes |
|---|---|---|
| `login` | `/login` | |
| `profile-skills` | `/profile` | Skills section, showing the real category/service dropdowns (task 205) |
| `profile-service-areas` | `/profile` | Service areas section, showing the real city/zone/pincode dropdowns (task 205) |
| `jobs` | `/jobs` | |
| `job-detail` | `/jobs/[id]` | |
| `earnings` | `/earnings` | |

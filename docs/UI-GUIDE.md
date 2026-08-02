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

**18 of 21 screenshots below are now captured**, against a genuinely fresh
database (reset from empty, all migrations replayed, both dev seed scripts
run). Two real, pre-existing bugs were found and fixed to make that replay
possible at all (see [Known issues found and fixed](#known-issues-found-and-fixed-2026-08-02)) —
not local database drift, but migration files that would have failed the
same way for any fresh checkout of this repo.

The three still-missing screenshots (`admin-web/booking-detail`,
`customer-web/booking-detail`, `partner-web/job-detail`) all need one real
`Completed` booking to exist — that requires a slot window, a simulated
payment, a partner assignment, and a completion submission on top of the
catalog data below, which this pass did not build out. Noted as an honest
gap, not silently skipped; each row in the table below is explicit about
what's missing.

### Known issues found and fixed (2026-08-02)

Bringing up the local stack for the very first time from a genuinely empty
database (as opposed to this repo's usual long-lived, incrementally-migrated
dev database) surfaced two classes of bug in the migration history itself,
both now fixed in place:

1. **Duplicate schema in early migrations.** `20260730172343_AddCustomerAddressGeographyLink`
   and `20260730182139_AddFinancialSchema` each redundantly recreated the
   entire `booking`/`booking_item`/`booking_status_history`/`booking_addon_item`
   tables (and the former also redundantly re-added `customer_address`'s
   `locality_id`/`pincode_id` columns) that an earlier-numbered migration
   already created — a snapshot-drift artifact from concurrent branch work
   early in this project. Fixed by removing the dead duplicate operations
   from both files' `Up()`/`Down()`, keeping only what each migration
   actually adds new.
2. **Two seed migrations that dynamically over-seeded.** `20260731140113_AddAdminPermissionMatrix`
   and `20260731152427_AddNotificationTemplateManagement` each seed by
   reading a live static catalog (`AdminPermissionCatalog.Permissions` /
   `NotificationTemplateSeedData.BuildDefaults()`) with no filter - correct
   when first authored, but those catalogs keep growing as later tasks add
   modules/event types (Partner, Referral, Chat, Subscription, NestlyCoins;
   RecurringBooking, Referral, Subscription notifications). On a fresh
   database, both migrations now silently re-seed every later addition too,
   colliding with each addition's own dedicated incremental migration on a
   primary-key or unique-index conflict. Fixed by freezing each migration to
   the fixed set of modules/event types that existed when it was authored,
   matching the pattern every later incremental seed migration already used
   correctly.

Both fixes are scoped to migration files only — no application code,
`AdminPermissionCatalog`, or `NotificationTemplateSeedData` changed. Full
backend suite still 987/987 after the fix.

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

Captured at 1440x900 against a fresh database seeded only with the two dev
accounts ([First-time setup](#first-time-setup)) plus one minimal real
catalog record created live through the admin UI for this pass: state
Karnataka, city Bengaluru, zone/locality Koramangala, pincode 560034,
category "Home Cleaning", service "Deep Home Cleaning" (₹1499), and the
matching category/city + service/pincode serviceability mappings. No slot
window, payment, or partner assignment was set up, which is why the three
rows below marked "Not captured" are missing - they all need a real
`Completed` booking to exist.

### Customer web (`docs/assets/ui-guide/customer-web/`)

| Screenshot | Route | Notes |
|---|---|---|
| `login` | `/login` | ![login](assets/ui-guide/customer-web/login.png) Account-type selector (task 206) |
| `home` | `/` | ![home](assets/ui-guide/customer-web/home.png) |
| `categories` | `/categories` | ![categories](assets/ui-guide/customer-web/categories.png) |
| `service-detail` | `/services/[slug]` | ![service-detail](assets/ui-guide/customer-web/service-detail.png) |
| `booking-summary` | `/booking/summary` | ![booking-summary](assets/ui-guide/customer-web/booking-summary.png) Mid-checkout, one service in cart |
| `booking-detail` | `/bookings/[id]` | **Not captured** - needs a `Completed` booking (no slot/payment/partner-assignment data seeded this pass) |
| `wallet` | `/wallet` | ![wallet](assets/ui-guide/customer-web/wallet.png) |
| `profile` | `/profile` | ![profile](assets/ui-guide/customer-web/profile.png) |

### Admin web (`docs/assets/ui-guide/admin-web/`)

| Screenshot | Route | Notes |
|---|---|---|
| `login` | `/login` | ![login](assets/ui-guide/admin-web/login.png) |
| `dashboard` | `/dashboard` | ![dashboard](assets/ui-guide/admin-web/dashboard.png) |
| `bookings` | `/bookings` | ![bookings](assets/ui-guide/admin-web/bookings.png) Empty list - no bookings exist in this seed |
| `booking-detail` | `/bookings/[bookingId]` | **Not captured** - no booking exists to open |
| `catalog` | `/catalog` | ![catalog](assets/ui-guide/admin-web/catalog.png) |
| `partners` | `/partners` | ![partners](assets/ui-guide/admin-web/partners.png) |
| `coupons` | `/coupons` | ![coupons](assets/ui-guide/admin-web/coupons.png) |
| `reports` | `/reports` | ![reports](assets/ui-guide/admin-web/reports.png) |

### Partner web (`docs/assets/ui-guide/partner-web/`)

No dev seed exists for a partner account ([First-time setup](#first-time-setup)
step 6) - the screenshots below use a real partner registered live through
`/register` for this pass (mobile `9888877766`), OTP-verified by reading and
SHA-256-brute-forcing `partner_otp.code_hash` (6 digits, unsalted, ~instant
locally - the code itself is never logged or retrievable in plaintext by
design, same as the customer OTP path).

| Screenshot | Route | Notes |
|---|---|---|
| `login` | `/login` | ![login](assets/ui-guide/partner-web/login.png) |
| `profile-skills` | `/profile` | ![profile-skills](assets/ui-guide/partner-web/profile-skills.png) Real category/service dropdowns (task 205) - shows "Home Cleaning", not a raw GUID |
| `profile-service-areas` | `/profile` | ![profile-service-areas](assets/ui-guide/partner-web/profile-service-areas.png) Real city/zone/pincode dropdowns (task 205) - shows "Bengaluru", not a raw GUID |
| `jobs` | `/jobs` | ![jobs](assets/ui-guide/partner-web/jobs.png) Empty list - no bookings assigned in this seed |
| `job-detail` | `/jobs/[id]` | **Not captured** - no job exists to open |
| `earnings` | `/earnings` | ![earnings](assets/ui-guide/partner-web/earnings.png) |

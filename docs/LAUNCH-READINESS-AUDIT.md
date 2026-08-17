# LAUNCH-READINESS-AUDIT.md

Evidence-based audit of what is **actually implemented** in this repository,
versus what [ORIENTATION.md](ORIENTATION.md), the module specifications and
`tasks.csv` claim.

## STATUS

Audit performed **2026-08-17** against commit `bcef432`, on branch
`claude/nestly-urban-company-comparison-66095c-x1drri`.

This audit was commissioned because three sources disagreed about what is
built, and [MARKET.md](MARKET.md) §5 identified "launch readiness is not
verifiable" as a High-severity business risk. It resolves that disagreement.

### What this audit can and cannot prove

**Method: static code evidence.** For each module the audit checked for the
presence and coherence of domain entities, EF Core configurations, database
migrations, application services, API endpoints, background job registration,
frontend pages and test files.

**The solution was not built and no tests were executed.** The .NET 8 SDK is
not installed in this environment, and the operator instructed that it not be
installed. Every grade below therefore means *"the code exists, is wired, and
is internally coherent"* — **not** *"it compiles, passes its tests, or behaves
correctly at runtime"*.

That distinction matters and is not a formality. This audit closes the
question *"does this module exist at all?"* — which was genuinely open and
answered wrongly by the documentation. It does **not** close the question
*"does this module work?"* A build-and-test run remains a required, and now
much cheaper, second step: see [§8](#8-what-to-do-next).

---

## 1. HEADLINE FINDING

**The documentation is drastically stale in one direction, and the backlog is
wrong in the other.**

- **ORIENTATION.md understates the codebase by an enormous margin.** It is
  dated `Last verified: 2026-08-01`, claims **73 of 221 tasks done**, names
  Phase 2 (Catalog) as the active phase at 2/26, and states plainly that there
  is *"no catalog, serviceability, booking, payments, slots, coupons,
  post-booking or admin panel"* and that `admin-web` is *"scaffolded only — no
  product screens"*. **All of these statements are false.** Every one of those
  modules exists in code, and `admin-web` has 42 pages.

- **The module specifications are stale in the same direction.**
  `PRODUCT-ENHANCEMENTS.md`, `REFERRAL.md` and `NESTLY-COINS.md` all carry a
  `STATUS: Not implemented` header. All three modules are implemented.

- **The backlog overstates in one specific place.** `tasks.csv` rows
  `#296`–`#300` (Phase 17, added 2026-08-07) scope recurring-services work as
  `todo` that **already exists in full** from Phase 10.

The practical consequence: **the earlier competitive analysis in MARKET.md
§5.1 was built on the stale documents and materially misstates Nestly's
product position.** Corrections are in [§7](#7-corrections-to-marketmd).

---

## 2. EVIDENCE BASE

Counts taken directly from the working tree.

| Measure | Count |
|---|---:|
| EF Core migrations (excluding `.Designer.cs`) | 66 |
| Domain files (`shared/Domain`) | 139 |
| EF persistence configurations | 176 |
| Application files, across 45 feature areas | 308 |
| API controllers (consumer 27 · admin 31 · provider 8) | 66 |
| HTTP endpoints (consumer 85 · admin 253 · provider 43) | 381 |
| Frontend apps | 3 |
| Frontend pages (admin 42 · customer 29 · provider 10) | 81 |
| Test methods (`[Fact]`/`[Theory]`) across 4 test projects | 1,119 |
| `tasks.csv` rows | 627 (567 done · 50 decomposed · 10 todo) |

Note that `tasks.csv` has grown from the 221 rows ORIENTATION.md describes to
627, as later phases were decomposed — the 50 `decomposed` rows are parents
split into subtasks, not lost work.

---

## 3. PER-MODULE VERIFICATION

Grades: **Verified** — entity, persistence, service, endpoint and test
evidence all present. **Partial** — present but with a named hole.
**Absent** — no implementing code found.

| Module | Claimed | Code evidence | Grade |
|---|---|---|---|
| Identity & auth | Done | 265 tests in `Identity.Tests`; auth controllers on all three APIs | **Verified** |
| Customer & addresses | Done | Entities, `CustomerManagement.Tests`, `customer-web` profile/address pages | **Verified** |
| Catalog & serviceability | ORIENTATION: *active, 2/26* | `Category`/`Service`/`ServiceAddOn` entities, admin + consumer controllers, `Serviceability` area, extensive tests | **Verified** — claim false |
| Geography (city/locality/pincode) | Done | `City`, `Locality`, `Pincode`, `CategoryCityMapping`; controllers on all three APIs | **Verified** |
| Pricing | ORIENTATION: *does not exist* | `CityPricingPolicy`, `PromotionalPrice`, price-calculation service + validator, tests | **Verified** — claim false |
| Slots & availability | ORIENTATION: *does not exist* | `Slots` area, capacity migration, blackout/window/policy tests | **Verified** — claim false |
| Booking core | ORIENTATION: *does not exist* | `Booking` aggregate + items/add-ons/status-history/snapshots, lifecycle, concurrency and immutability tests | **Verified** — claim false |
| Payments, escrow, refunds, wallet | ORIENTATION: *does not exist* | `PaymentTransaction`/`PaymentAttempt`, escrow ledger, commission calculator, webhook + reconciliation tests | **Verified** — claim false |
| Coupons | ORIENTATION: *does not exist* | `Coupon` + redemption/segment/counter entities, admin and consumer controllers, tests | **Verified** — claim false |
| Post-booking (cancel, reschedule, review, support) | ORIENTATION: *does not exist* | Cancellation fee calculator, reschedule fee calculator, review moderation, support tickets + disputes, QA suite tests | **Verified** — claim false |
| Admin panel | ORIENTATION: *scaffolded only* | 31 admin controllers, 253 endpoints, RBAC permission matrix, 42 `admin-web` pages | **Verified** — claim false |
| Provider / partner (Phase 7) | In implementation | `Provider`, auth identity, availability windows, background check, earnings; `provider-api` (8 controllers) and `provider-web` (10 pages) | **Verified** |
| Referral (Phase 9) | Spec: *Not implemented* | `Referral`, milestones, awards, program config; 7 referral test files; `refer-earn` page; permission + template seed migrations | **Verified** — spec stale |
| Subscription (Phase 10) | Spec: *Not implemented* | `SubscriptionPlan`, `CustomerSubscription`, billing cycle; `AddSubscriptionSchema` migration; consumer + admin controllers; `subscription` page | **Verified** — spec stale |
| Recurring bookings (Phase 10) | Spec: *Not implemented*; backlog `#296`–`#300`: *todo* | `RecurringBookingPlan`/`Occurrence`/`AddOn` entities; `AddRecurringBookingPlan` migration; Hangfire job via `ScheduleRecurringBookingJob`; 6 endpoints; `recurring-bookings` + `/new` pages; scheduler + plan tests | **Verified** — spec *and* backlog both wrong |
| In-app chat (Phase 10) | Spec: *Not implemented* | `ChatThread`/`ChatMessage`, `AddChatSchema` + permission-seed migrations, consumer and admin controllers, SignalR hub, tests | **Verified** — spec stale |
| Completion verification (Phase 10) | MARKET.md: *unbuilt* | `BookingCompletionProof` entity, `AddBookingCompletionProof` migration, dedicated consumer controller, support tests | **Verified** — claim false |
| Nestly Coins (Phase 11) | Spec: *Not implemented* | `NestlyCoins` domain folder, program config, schema migration, consumer + admin controllers, 3 test files | **Verified** — spec stale |
| Automatic provider assignment (Phase 14) | MARKET.md: *deferred* | `ProviderAutoAssignmentHandler`, matching service with route ranking, eligibility + travel-feasibility services, double-booking guard, 6 test files | **Verified** — claim false |
| Order tracking (Phase 16) | Done, 27/32 | `BookingTracking`, provider location pings + coordinates migration, SignalR hub, Google Maps route provider, `bookings/[id]/track` page, 9 test files | **Partial** — 5 known defects open (`#291`–`#295`) |
| **B2B / organisation model** | MARKET.md: *Critical gap* | No `Organisation`, `Tenant`, `Contract`, `Site`, `PurchaseOrder` or `Invoice` type anywhere in `shared/Domain` | **Absent** — confirmed |
| **WhatsApp booking intake** | MARKET.md: *High gap* | WhatsApp appears only as a `CustomerCommunicationPreference` notification channel. No booking intake path | **Absent** — confirmed |
| **AMC / entitlement model** | MARKET.md: *Critical gap* | Subscription and recurring plans exist, but no prepaid-entitlement drawdown, preventive-visit schedule or renewal pipeline | **Absent** — confirmed |

---

## 4. DOCUMENTATION INTEGRITY FINDINGS

Each of these is a concrete, fixable defect in the documentation suite.

1. **ORIENTATION.md is ~16 phases behind the code.** Last verified
   2026-08-01, but its content reflects a state well before that — it
   describes Phase 2 as active while Phases 2–16 are implemented. It is
   designated by `README.md` as *"the only document describing current
   repository state"*, which makes it the single most load-bearing document in
   the suite and the one most damaging when wrong.

2. **Four module specifications carry false `STATUS` headers.**
   `PRODUCT-ENHANCEMENTS.md`, `REFERRAL.md` and `NESTLY-COINS.md` say *"Not
   implemented"* for modules that are implemented; `PROVIDER.md` says *"In
   implementation"* for one that appears complete.

3. **`README.md`'s topic-ownership model has no status owner.** Because
   "current state" belongs to ORIENTATION.md but each spec also carries its own
   `STATUS` section, status is duplicated across five documents with no
   mechanism keeping them consistent. This is the structural cause of finding
   1 and 2, not bad luck — the suite's own CHANGE POLICY forbids exactly this
   kind of duplication.

4. **No document records the ~60 previously-falsified completions.**
   MARKET.md §5.2 refers to roughly sixty tasks once marked done that were
   never implemented. That history is real and consequential, but it lives
   only in commit history — which is why the *opposite* error (this audit's
   finding) was not caught for two weeks.

---

## 5. BACKLOG INTEGRITY FINDINGS

**Rows `#296`–`#300` duplicate shipped work.** Added 2026-08-07 in commit
`bcef432` with the rationale *"differentiation vs Urban Company: recurring/
subscription home-local services"*, they scope:

| Row | Scopes | Already exists as |
|---|---|---|
| 296 | Recurring booking schema and rule model | `RecurringBookingPlan`, `RecurringBookingOccurrence`, `RecurringBookingPlanAddOn`, `RecurringBookingRecurrenceFrequency` + `AddRecurringBookingPlan` migration |
| 297 | Recurring booking generation service | `IRecurringBookingSchedulerService` + Hangfire registration in `RecurringBookingJobScheduleExtensions` |
| 298 | Customer subscription management API and UI | `RecurringBookingPlansController` (6 endpoints) + `recurring-bookings`, `recurring-bookings/new` pages |
| 299 | Admin recurring plan visibility | Admin subscription/plan controllers |
| 300 | Provider-side recurring job visibility | `provider-web` jobs surface |

These five rows should be closed as already-delivered after a functional
check, not implemented. **They were the basis for MARKET.md's
recommendation to "ship subscription and recurring bookings" as the fastest
path to differentiation — that recommendation is now void.** The
differentiator already exists; the open question is whether it works, and
whether anyone can buy it.

**The 10 genuinely-open rows** are `#291`–`#295` (Phase 16 tracking defects:
unretried at-most-once notifications, provider photo/rating with no backing
data, a `ChatHubBroadcastHandler` exception path, and a `ProviderAssigned`
notification timing error) and `#296`–`#300` (the duplicates above). Note that
`#291`–`#295` are real defects, several of them customer-visible.

---

## 6. TEST COVERAGE ASSESSMENT

1,119 test methods is a genuinely strong number for a pre-launch product, but
the distribution is uneven and the packaging is misleading.

| Project | Tests | Files | Real scope |
|---|---:|---:|---|
| `Catalog.Tests` | 834 | 123 | **Misnamed.** Covers booking, payments, escrow, refunds, wallet, coupons, slots, pricing, provider matching and auto-assignment, tracking, chat, referral, subscriptions, recurring bookings, Nestly Coins, reporting, dashboard, CMS and admin workflows |
| `Identity.Tests` | 265 | — | Identity and auth |
| `CustomerManagement.Tests` | 12 | — | Customer management — thin relative to the module |
| `Performance.Tests` | 8 | — | Performance smoke |

Two observations:

- **`Catalog.Tests` is doing the work of a dozen suites under one name.** At
  123 files it is effectively the platform's main regression suite. The name
  actively misleads: a reader checking "is booking tested?" would look for
  `Booking.Tests`, find nothing, and conclude no. Splitting it along module
  lines is a mechanical, low-risk change with real navigational value.
- **`CustomerManagement.Tests` at 12 methods is thin** for a module covering
  profile, communication preferences and notes.

**Coverage was not measured.** No test run was performed, so these are counts
of test methods, not evidence of passing tests or of line/branch coverage.

---

## 7. CORRECTIONS TO MARKET.md

[MARKET.md](MARKET.md) §5.1 was written against the stale documentation and
overstates the product gap. It has been corrected in place; recorded here for
traceability.

| MARKET.md claim | Reality |
|---|---|
| "Subscription module unbuilt — Critical" | Built (entities, migration, controllers, page, tests) |
| "Recurring bookings unbuilt — Critical" | Built end-to-end including the Hangfire generation job |
| "Completion verification unbuilt — High" | Built (`BookingCompletionProof` + migration + controller) |
| "Referral engine unbuilt — Medium" | Built (7 test files, milestones, program config) |
| "In-app chat unbuilt — Medium" | Built (schema, hub, both APIs) |
| "Automatic provider assignment deferred — High" | Built (matching, eligibility, travel feasibility, double-booking guard) |

**What survives unchanged.** The three Critical *business-model* gaps are
confirmed absent in code: **no B2B/organisation model**, **no AMC/entitlement
model**, **no WhatsApp booking intake**. So are all six business and
operational gaps in §5.2 — unit economics, pricing strategy, supply
acquisition, liability/insurance, GST posture and local ops footprint — none
of which are code questions.

**The strategic conclusion is unchanged and, if anything, strengthened.**
MARKET.md argued the money is in contracts rather than one-off consumer jobs.
Nestly turns out to be much closer to a shippable consumer product than the
documents suggested — which means less consumer engineering stands between
today and the contract thesis, and the B2B account model is now more clearly
*the* binding constraint rather than one of several.

---

## 8. WHAT TO DO NEXT

Ordered by dependency.

1. **Build the solution and run the four test suites.** This is the one thing
   this audit could not do, and every grade above is provisional until it
   happens. It requires only the .NET 8.0.422 SDK from `global.json`.
2. **Rewrite ORIENTATION.md §2 against this audit.** It is the designated
   state-of-the-repository document and is currently the most misleading file
   in the suite.
3. **Correct the four stale spec `STATUS` headers** in
   `PRODUCT-ENHANCEMENTS.md`, `REFERRAL.md`, `NESTLY-COINS.md` and
   `PROVIDER.md`.
4. **Close `tasks.csv` rows `#296`–`#300`** as already-delivered, after a
   functional check of the recurring-booking flow.
5. **Fix the five open Phase 16 defects** (`#291`–`#295`). These are real and
   several are customer-visible.
6. **Give status a single owner.** Remove per-spec `STATUS` sections in favour
   of one status table in ORIENTATION.md, or make them a one-line pointer to
   it. Duplicated status is what produced this entire class of error.
7. **Split `Catalog.Tests`** along module lines so test coverage is legible
   from the project layout.
8. **Then proceed to the B2B account model design** — with the audit now
   showing it to be the principal remaining engineering constraint on the
   revenue strategy.

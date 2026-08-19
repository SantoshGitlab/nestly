# LAUNCH-READINESS-AUDIT.md

Verification of the documentation suite's status claims against the code, and
the record of the corrections applied.

## STATUS

Audit performed **2026-08-17** against `main` at commit `e020819`.

Commissioned because [MARKET.md](MARKET.md) §5 identified *"launch readiness is
not verifiable"* as a High-severity business risk, on the grounds that
ORIENTATION.md, the module specifications and `tasks.csv` disagreed about what
was built.

### What this audit can and cannot prove

**Method: static code evidence.** For each module the audit checked for the
presence and coherence of domain entities, EF Core configurations, migrations,
application services, API endpoints, background job registration, frontend
pages and test files.

**The solution was not built and no tests were executed** — the .NET 8 SDK is
absent from the audit environment and the operator instructed that it not be
installed. Every statement below about existence means *"the code is present
and wired"*, never *"it compiles, passes, or behaves correctly"*.

The most recent evidence of a working build is ORIENTATION.md's 2026-08-08
pass against commit `39cc78a`: `dotnet build` 0 errors / 0 warnings,
`dotnet test` 1767 passing / 0 failing. That is 85 commits behind `e020819`,
so it does not cover Phase 18.

---

## 1. A CORRECTION TO THIS AUDIT'S OWN FIRST DRAFT

The first version of this document, written earlier the same day, reported
that ORIENTATION.md *"understates the codebase by roughly sixteen phases"* —
that it claimed 73/221 tasks done and denied the existence of booking,
payments, coupons, post-booking and the admin panel.

**That finding was an artifact of auditing a stale branch.** It was performed
against commit `bcef432`, which was 85 commits behind `main`. On `main`,
ORIENTATION.md had already been re-verified on 2026-08-08 — against a real
build and test run — and was substantially accurate.

The lesson is not incidental to this audit's subject. An audit that does not
first confirm it is looking at the current default branch can manufacture
exactly the class of error it was commissioned to find. The corrected findings
below are all verified against `e020819`.

---

## 2. EVIDENCE BASE

Counts taken from the working tree at `e020819`.

| Measure | Count |
|---|---:|
| Migration classes (`database/migrations`, excluding designers/snapshots) | 76 |
| Domain entity classes (deriving `Entity<>`/`AggregateRoot<>`) | 93 (15 aggregate roots) |
| EF entity configurations · `DbSet`s | 94 · 72 |
| Application files, across 47 feature areas | 341 |
| API controllers (consumer 27 · admin 37 · provider 9) | 73 |
| HTTP endpoints (consumer 85 · admin 289 · provider 49) | 423 |
| Frontend pages (admin 49 · customer 29 · provider 10) | 88 |
| Test methods (`[Fact]`/`[Theory]` declarations) | 1,363 |
| `tasks.csv` rows | 647 (596 done · 50 decomposed · 1 todo) |

Note on the test figure: 1,363 counts *declarations*, whereas ORIENTATION.md's
1,767 counts *executed cases* from a real run — `[Theory]` methods expand into
several. The two are consistent, not contradictory.

---

## 3. FINDINGS

### 3.1 Confirmed accurate

ORIENTATION.md's substantive claims hold. Every module it lists as live has
code behind it: identity, catalog, serviceability, slots, booking, payments,
wallet, coupons, cancellation/reschedule, reviews, support, notifications,
admin panel with RBAC, provider/partner, referral, Nestly Coins, subscriptions,
recurring bookings, chat, live tracking and completion verification.

Several of these were reported as *unbuilt* in MARKET.md's first draft. They
are not. See [§5](#5-corrections-applied-to-marketmd).

### 3.2 Four specifications carried false status headers — **fixed**

`PRODUCT-ENHANCEMENTS.md`, `REFERRAL.md` and `NESTLY-COINS.md` read *"Not
implemented"*, and `PROVIDER.md` read *"In implementation"*, for modules
delivered phases earlier. This was the one documentation defect that was
genuinely live on `main`, and it is what misled the competitive analysis.

Each now states the delivering phase and defers to ORIENTATION.md for status.

### 3.3 Status had no single owner — **fixed**

The structural cause of 3.2: `README.md` designated ORIENTATION.md as the only
document describing repository state, while every module spec also carried its
own `STATUS` section, with nothing keeping the two consistent. Duplication of
this kind is already forbidden by the suite's own CHANGE POLICY; status was
simply never named as a topic under it.

`README.md` now carries an explicit rule — *implementation status has exactly
one owner* — and records why.

### 3.4 Ten documents were missing from the index — **fixed**

`BOOKING-FLOW-AUDIT.md`, `CATALOG-ARCHITECTURE-REVIEW.md`,
`ENHANCEMENT-BACKLOG-2026-08-08.md`, `PHASE-12-HANDOFF.md`,
`PHASE-16-CLOUD-BRIEF.md`, `QA-REPORT-2026-08-07.md`, `RUNBOOK-DEPLOYMENT.md`,
`UAT-REPORT.md`, `migrations-audit.md` and `migrations-plan.md` existed in
`docs/` but appeared nowhere in the index that claims to be the suite's entry
point.

Two of those omissions mattered a great deal: `QA-REPORT-2026-08-07.md` holds
the current release verdict, and `ENHANCEMENT-BACKLOG-2026-08-08.md` is
described by ORIENTATION.md as the place the next task comes from. All ten are
now indexed.

### 3.5 ORIENTATION.md's figures had drifted — **fixed**

Accurate as of 2026-08-08, stale by 2026-08-17 because Phase 18 landed in
between. Corrected:

| Claim | Was | Now |
|---|---|---|
| Backlog | 627 rows, 577 done, **zero** todo | 647 rows, 596 done, **1** todo |
| Phases | "all eleven phases (0–10, plus 11)" | 0–18, plus unphased setup rows |
| `admin-api` | 32 controllers | 37 controllers / 289 endpoints |
| Migrations | 140 | 76 (140 counted files, including designers) |
| Domain | 141 entities | 93 entity classes across 145 files |

### 3.6 `tasks.csv` row 302 was corrupt — **fixed**

The `notes` field used backslash-escaped quotes (`\"420px\"`). CSV has no
backslash escape: the field terminated early, the remainder of the note was
parsed as the `phase` column, and the row's real phase was lost. Four scripts
(`task_worker.py`, `task_claim.py`, `sync_autopilot_tasks.py`,
`init_worker_project.py`) parse this file, and `admin-web` types mirror it.

Escaping corrected to doubled quotes, the truncated note restored, and the
phase recovered as `Phase 6 - Admin Panel` from commit `a7b3f18`. All 647 rows
now parse.

### 3.7 `TASKS-SUMMARY.md` was six phases behind — **fixed**

Its table stopped at Phase 11 and reported 522 rows against an actual 647.
Regenerated with all nineteen groups and the open row called out.

### 3.8 Release readiness is not backlog completeness — **open**

The one remaining `todo` is **task 318**: execute QA phases 3 and 4 from
`QA-REPORT-2026-08-07.md`. That report's verdict is **NO-GO for release on
absence of evidence, not on known defects** — 587 inventoried UI features are
runtime-unverified, and cross-service booking consistency between the three
backends is unmeasured.

This is the honest answer to *"what is shippable"*: the backlog is closed, the
code is present, the last full test run was green 85 commits ago, and nobody
has verified the product end to end at runtime. **MARKET.md's High-severity
"launch readiness is not verifiable" risk is therefore narrowed but not
retired** — it is no longer a documentation problem, it is an outstanding QA
execution.

---

## 4. CONFIRMED GAPS (unchanged)

Re-verified absent at `e020819`, by search across `shared/Domain`,
`shared/Application` and all three API surfaces:

- **No B2B / organisation model.** No `Organisation`, `Contract`, `Site`,
  `PurchaseOrder` or `Invoice` type exists. The model runs *person → booking*.
- **No AMC / entitlement model.** Subscription and recurring plans exist;
  prepaid entitlement drawdown, preventive-visit scheduling and a renewal
  pipeline do not.
- **No WhatsApp booking intake.** WhatsApp appears only as a
  `CustomerCommunicationPreference` notification channel.

All six business and operational gaps in MARKET.md §5.2 also stand — unit
economics, pricing strategy, supply acquisition, liability/insurance, GST
posture and local ops footprint. None are code questions.

---

## 5. CORRECTIONS APPLIED TO MARKET.md

MARKET.md §5.1 was written against the four false spec headers and overstated
the product gap. Corrected in place; recorded here for traceability.

| MARKET.md claim | Reality |
|---|---|
| "Subscription module unbuilt — Critical" | Built (Phase 10) |
| "Recurring bookings unbuilt — Critical" | Built (Phase 10, extended Phase 17), including the Hangfire generation job |
| "Completion verification unbuilt — High" | Built (`BookingCompletionProof` + migration + controller) |
| "Referral engine unbuilt — Medium" | Built (Phase 9) |
| "In-app chat unbuilt — Medium" | Built (Phase 10, SignalR) |
| "Automatic provider assignment deferred — High" | Built (Phase 14) |

**The strategic conclusion is unchanged and strengthened.** MARKET.md argued
the money is in contracts rather than one-off consumer jobs. Nestly is far
closer to a shippable consumer product than its own documents suggested, which
means less consumer engineering stands between today and the contract thesis —
and the B2B account model is now unambiguously *the* binding engineering
constraint on the revenue strategy, rather than one of several.

---

## 6. WHAT REMAINS

1. **Execute task 318** — the QA report's phases 3 and 4. Until then the
   release verdict stands at NO-GO and no launch date is defensible.
2. **Build and test `main` at its current head.** The last green run predates
   Phase 18 by 85 commits. This audit could not do it.
3. **Split `Catalog.Tests`.** At 123 files and ~1,049 declared test methods it
   is the platform's main regression suite under a name that describes one
   module — a reader checking "is booking tested?" would look for
   `Booking.Tests`, find nothing, and conclude wrongly.
4. **Re-verify `CustomerManagement.Tests`.** 12 test methods is thin for a
   module covering profile, communication preferences and notes.
5. **Then proceed to the B2B account model design**, now the principal
   remaining engineering constraint on the revenue strategy.

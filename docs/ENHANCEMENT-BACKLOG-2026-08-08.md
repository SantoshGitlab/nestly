# Enhancement Backlog — 2026-08-08

Work remaining **after** `tasks.csv` was exhausted. Every item below was
verified against the code on `main` during this pass — file:line evidence is
given for each.

**These are now real backlog rows: `tasks.csv` `#303`–`#320`, "Phase 18 -
Spec-Gap Closure".** Each section below names its row. Claim with
`scripts/task_claim.py` before starting; several sessions work this repo at
once.

Ordered by leverage (delivered value ÷ cost), not by module.

---

## 0. STATE OF `main` (verified this pass)

| Check | Result |
|---|---|
| `dotnet build Nestly.sln` (clean checkout of `origin/main`) | **0 errors, 0 warnings** |
| `dotnet test Nestly.sln` (same, before the §0.1 fix, at `39cc78a`) | 1765 passed, **1 failed**, 1766 total |
| `dotnet test Nestly.sln` (same, after the §0.1 fix, on `08752d6`) | **1767 passed, 0 failed** |

Verified in throwaway worktrees built from `origin/main`, **not** the working
tree — during this pass the working tree carried another session's in-flight
changes (§0.2).

### 0.1 A failing test on `main` — found and fixed (`#303`)

```
Nestly.Catalog.Tests.NotificationTemplateManagementServiceTests
  .Deactivating_a_template_makes_the_renderer_stop_supporting_it_once_the_cache_is_invalidated
SqliteException : SQLite Error 1: 'no such function: ANY'
```

Cause — `NotificationTemplateRepository.KnownEventTypeOnly()`, as shipped by
`088ba63` ("Isolate one bad notification_template row from crashing all
dispatch"):

```csharp
var validNames = Enum.GetNames<NotificationEventType>();
return _context.Set<NotificationTemplate>()
    .FromSqlInterpolated($"SELECT * FROM notification_template WHERE event_type = ANY({validNames})");
```

`= ANY(array)` is PostgreSQL-only and `Catalog.Tests` is SQLite-backed, so every
caller of `ListActiveAsync` was untestable there. The production intent was
sound; the raw SQL was not portable to the test provider. Same class of bug
ORIENTATION.md §5 warns about — raw SQL bypasses conventions and survives
`dotnet build` — but with provider dialect rather than snake_case.

**Fixed 2026-08-08** by expressing the filter in LINQ instead:

```csharp
var validEventTypes = Enum.GetValues<NotificationEventType>();
return _context.Set<NotificationTemplate>()
    .Where(t => validEventTypes.Contains(t.EventType));
```

EF applies the property's string value converter to each element and emits a
standard `event_type IN (...)` both providers run identically. It still filters
in SQL before materialization — the whole point — and needs no raw SQL, so it
also avoids the `EF1002` analyser warning that a raw-SQL `IN` fix would have
introduced.

Two things worth keeping in mind, because they are why this shipped broken:

- **The defensive behaviour had no test of its own.** It could not have had one
  — the filter crashed on SQLite, so nothing exercising it could pass. A
  regression test now inserts an unrecognized `event_type` row by raw SQL and
  asserts `ListActiveAsync`, `ListAsync` and the renderer all survive it.
- **That test is falsifiable, and was checked.** With the filter removed it
  fails with the exact production failure mode: *"Cannot convert string value
  'AnEventTypeThisEnumNoLongerDefines' from the database to any value in the
  mapped 'NotificationEventType' enum."*

### 0.2 Concurrent session's work — since landed

While this pass ran, another session was mid-feature in the working tree
(17 modified files, timestamped 22:34–22:52 on 2026-08-08). It has since been
committed and pushed as `08752d6`, "Add camera photo upload for job-completion
verification": `IFileStorageService` + `LocalDiskFileStorageService`,
`IProviderJobService.UploadCompletionPhotoAsync`, a `JobsController` upload
endpoint, and the provider-web job-detail upload UI.

**Note the overlap:** item 10 (`#314`, admin media upload) is the natural second
consumer of `IFileStorageService` and must reuse it rather than introducing a
second storage path.

### 0.3 The original backlog is exhausted

`tasks.csv` reached 629 rows with **zero `todo`** — 577 `done` and 50
`decomposed` parent placeholders, each already satisfied by its own lettered
subtasks (backlog convention). Two low-priority admin table-UX rows (`#301`,
`#302`) were added during this pass by another session, and this document's
items were then appended as `#303`–`#320` under "Phase 18 - Spec-Gap Closure".

One stale claim file remains (`.task-claims/277.claim`, 2026-08-07, well past
the 90-minute TTL) — see `#320`.

---

## TIER 1 — Backend already ships it; frontend never calls it

Highest ROI in the repository. Each is a few hours of frontend work against an
API that is already built, tested and merged.

### 1. Referral code at customer registration — `#305` ⭐ highest-value single item

The backend implements referred-signup attribution **end to end**:

- `RegistrationContracts.cs:19` — `RegisterCustomerRequest(..., string? ReferralCode = null)`
- `RegistrationValidators.cs:43-45` — validated when present
- `CustomerRegistrationService.cs:147-176` — `TryCreateReferralAsync`, resolves
  the referrer via `GetByReferralCodeAsync`, honours program-active state, and
  fails soft (logs, never blocks registration)

`frontend/customer-web/src/app/register/page.tsx` collects mobile, OTP, name,
email, password, consent — **and never sends `referralCode`**. The only referral
surface in customer-web is `/refer-earn`, which is post-signup.

Consequence: a referred customer cannot be attributed at sign-up, so Phase 9's
entire referral loop — codes, milestones, expiring wallet credit, the fraud
review queue — has no inbound path in the product. Sixteen completed tasks are
sitting behind one missing form field.

**Scope:** add the field (plus `?ref=` URL prefill, since shared links carry the
code), send it, surface the "invited by" confirmation. Frontend only.

### 2. Admin account-unlock control — `#306`

`AdminAuthController.cs:68-75` — `POST /admin/auth/unlock/{adminUserId}`, gated
behind `settings.write`, calling `_loginService.UnlockAsync`. Built and
authorized.

admin-web **displays** lockout state and offers no way to clear it. An admin
locked out by the throttling policy stays locked until the lock expires or
someone writes SQL. Frontend only.

### 3. Provider push-notification registration — `#307`

`backend/provider-api/ProviderApi/Controllers/DeviceTokensController.cs` exists
(task 277, `done`). `frontend/provider-web/src` contains **no** `deviceToken`,
no `Notification.requestPermission`, no service worker — grepped, zero hits.

Providers therefore receive no push at all, and the dispatch path that would
reach them is live but unreachable. Given that job offers are time-sensitive,
this is the largest functional hole in provider-web. Frontend only (plus a
service worker).

---

## TIER 2 — Entity and enforcement exist; a thin API + UI is missing

### 4. Provider capacity has no write path anywhere — `#308`

`ProviderCapacity` (`backend/shared/Domain/ProviderCapacity.cs`) exists with
`MaxJobsPerDay`/`MaxJobsPerSlot`, a repository, and live enforcement in
`IProviderAssignmentEligibilityService`. Tests cover the limits
(`ProviderAssignmentEligibilityServiceTests.cs:156,182,211`).

Nothing in the codebase can create a row. The code says so itself, in
`IProviderScheduleConflictService.cs:21-22`:

> "…a `ProviderCapacity` row, which today none of them do (nothing in this
> codebase can create one — see `IProviderCapacityRepository`)."

And `ProviderDoubleBookingTests.cs:228` pins the current reality:
`context.Set<ProviderCapacity>().Should().BeEmpty("this is the state every
provider is in today")`.

So every provider is permanently unlimited, and a working, tested capacity
engine is dead code. **Scope:** admin-api CRUD endpoint + admin-web control on
the provider detail page; optionally a read-only view in provider-web.

Note the entity's own doc comment calls capacity "advisory only in v1" per
PROVIDER.md OPEN DECISIONS #1 — confirm that decision still holds before
building, since the eligibility service now enforces rather than advises.

### 5. Provider rating / performance view — `#309`

Task 293 landed provider-scoped review ratings. `provider-api` exposes no
rating endpoint (`ProfileController` — zero `rating` hits) and provider-web
renders none. Providers cannot see their own score.

`ProviderManagementContracts.cs:127-128` records that a `provider_rating_summary`
rollup is deliberately out of scope (PROVIDER.md OPEN DECISIONS #4) — so compute
from review history rather than adding a rollup table.

---

## TIER 3 — Genuine full-stack gaps in shipped features

### 6. Wallet credit at checkout (SRS 11.7.2) — `#310` ⭐ user-facing contradiction

`BookingContracts.cs:69` states plainly: *"Booking summary data (SRS 11.7.2).
Wallet credit is omitted."* The booking-summary page has no wallet line and no
apply control (grepped `frontend/customer-web/src/app/booking*` — wallet appears
only in refund copy).

Meanwhile `/wallet` tells the customer their balance is **"applied
automatically at checkout."** It is not. The product currently makes a promise
it does not keep, and wallet balance accrued from refunds, referral rewards and
coins has no redemption path.

This is the highest-severity trust gap in the customer flow — the money is real
and the customer has been told it will be used. Needs pricing-step integration
plus UI.

### 7. Admin Payment Transaction View (SRS 12.13.1) — `#311`

`admin-api/Controllers/` has 32 controllers and **no `PaymentsController`**;
admin-web has no `payments` route. consumer-api has one, so the data exists.

Payments are visible to ops only incidentally, through booking detail. There is
no transaction list, no filtering by gateway status, no reconciliation surface —
this is the single largest documented-but-unbuilt admin module.

### 8. Provider↔customer chat (2 of 3 apps built) — `#312`

`ChatController` exists in both consumer-api and admin-api; `provider-api` has
none, and provider-web has zero `chat` references. The customer can open a
booking thread the assigned provider cannot see or answer.

Needs provider-api endpoints + provider-web UI. The SignalR transport, thread
model and offline-notification fallback all already exist
(PRODUCT-ENHANCEMENTS.md §3).

### 9. Admin role CRUD and permission-matrix editor (SRS 12.2.2 / 12.2.3) — `#313`

`AdminUsersController` exposes `GET /roles` (line 76) and user-level
create/update/role-assign/activate/deactivate/reset-password — but no role
create/update and no role↔permission mapping edit. The nine named roles and the
permission matrix are seed-time constants; changing who may do what requires a
migration. Backend + frontend.

### 10. Media / file upload in admin-web — `#314`

`grep 'type="file"'` across all three frontends hits **only** provider-web
(`KycSection.tsx:205`, `PhotoSection.tsx:170`, `jobs/[id]/page.tsx:815`).
admin-web has none — CMS banners and pages accept a URL string only, so an admin
cannot actually publish an image.

**Sequencing:** this is the natural consumer of the `IFileStorageService`
landing in §0.2. Build it after that merges, and reuse it.

---

## TIER 4 — Correctness, documentation and process debt

11. ~~**Fix §0.1.**~~ **Done — `#303`.** `main` was not green; every future
     session would have inherited a failing suite and learned to ignore it.
     Fixed and covered by a falsifiable regression test (§0.1).

12. **`JobStatus` ordinal mirroring in provider-web** — `#315` (QA §8). The enum is
     hand-mirrored against backend ordinals — and per [[nestly-phase-16-branch]]
     enums here cross the wire as ordinals by design, so a reorder silently
     mislabels every badge. Generate it, or pin it with a contract test.

13. **`docs/API.md` documents zero endpoints** — `#316`. All 47 sections are conventions
     (naming, versioning, pagination, errors). Not one route across three APIs
     is specified. Anyone writing tests from the docs covers nothing — precisely
     the blind spot QA §7 named.

14. ~~**`docs/ORIENTATION.md` is materially stale.**~~ **Done — `#304`.** The
     document the README sends every newcomer to first was "last verified
     2026-08-01" and claimed Phase 2 was active at 73/221 tasks with "no
     catalog, serviceability, booking, payments, slots, coupons, or admin
     panel", while all eighteen phases were in fact closed. Rewritten §§2, 4, 7
     and 8 against the code. It also had one claim backwards: domain events are
     live now (16 handlers over 8 event types), while the MediatR
     `ICommand`/`IQuery` pipeline is the part with zero implementations.

15. **No admin-web browser E2E suite** — `#317` (UAT-REPORT.md gap #1). Admin acceptance
     is verified at the API layer only. customer-web has `e2e/`; admin-web has
     nothing.

16. **QA Phases 3 and 4 never executed** — `#318` (QA-REPORT §9). All 587 inventoried
     features are runtime-unverified, and cross-service contract drift between
     the three backends is unmeasured. The 2026-08-07 verdict is **NO-GO for
     release**, and the stated blocker is absence of evidence, not known
     defects.

17. **Housekeeping** — `#320`: 27 git worktrees are registered, most of them stale
     per-task/per-agent scratch (`git worktree list`). One stale claim file
     (`.task-claims/277.claim`). Both are cheap to sweep.

---

## CORRECTION TO THE 2026-08-07 QA REPORT

QA §6 lists, in bold, *"approved-provider gating — a `PendingVerification`
provider can open every screen; `Suspended` gets only a passive alert."* That
finding was static-only and reads as a security hole. It is not one:

- `ProviderLoginService.cs:150` — `Suspended` and `Deactivated` are **refused at
  login**. They never get a token.
- `ProviderLoginService.cs:19-23` — `PendingVerification` is allowed in
  **deliberately and documentedly**, because a newly registered provider must be
  able to sign in to complete onboarding and submit KYC.
- `ProviderMatchingService.cs:119` — assignment considers `Active` providers
  only, so a pending provider cannot receive work regardless of which screens
  render.

The real gap is UX, not access control: a pending provider sees job/earnings
screens that can never populate, with no explanation of what they are waiting
for. Worth fixing as onboarding messaging — not as a security defect.

Also stale in that report: it cites `tasks.csv` 293 as `todo`; 293 is `done` and
merged (`4280db7`).

---

## SUGGESTED ORDER

1. ~~§0.1 — get `main` green~~ — **done, `#303`**
2. ~~Item 14 — fix ORIENTATION.md~~ — **done, `#304`**
3. Items 1, 2, 3 (`#305`, `#306`, `#307`) — Tier 1 frontend-only wins against
   already-shipped APIs
4. Item 6 (`#310`) — wallet at checkout, closing a promise the product already
   makes to customers
5. Items 4, 7 (`#308`, `#311`) — capacity write path, admin payments view
6. Items 8, 9, 10 (`#312`, `#313`, `#314`) — remaining full-stack gaps
7. Items 15, 16 (`#317`, `#318`) — the evidence needed to move off NO-GO

Steps 3–5 are small and mutually independent, which suits the parallel-session
pattern this repo already runs. `#318` is the one item with a hard external
blocker (a connected browser extension) and seed-data prerequisites, so start
it early even though it finishes last.

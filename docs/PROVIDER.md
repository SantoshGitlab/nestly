# PROVIDER.md

Provider / Vendor module specification.

## STATUS

**In implementation.** The open decisions below are resolved (task 144); the
data model (tasks 145a-145f) and provider auth/onboarding foundation (tasks
146a-146c) are being built against those decisions. Out of scope for Phase 1
per the SRS (§4.2 Excluded Direct End-User Interfaces, §34 Open Decision #9)
— this is the SRS's own release-phase terminology, unrelated to the
backlog's numbered phases below.

Automatic provider assignment — deferred by decision 1 below at the time —
is now in implementation as its own **Phase 14** (`tasks.csv` 242-250); see
OPEN DECISIONS — AUTOMATIC ASSIGNMENT below for that phase's decisions.

In the backlog (`tasks.csv`), Provider is scheduled as **Phase 7**, ahead of
Hardening & Launch (Phase 8) — moved there explicitly so provider/provider
work is done before launch, not after it. No longer "deferred" in the sense
of "after everything else"; only in the sense of "not yet built."

## PURPOSE

Nestly connects customers to services, but a person must actually fulfill each booking. This document defines the **Provider** (service provider) module: identity, availability, assignment, earnings, and how it integrates with the existing Customer/Booking/Admin domains without breaking module boundaries.

Note on terminology: the SRS uses "vendor" only to mean external third-party providers (payment gateway, SMS/Email/WhatsApp). The platform role described here — the person or company who fulfills a booking — is called **Provider**, matching the module name already listed in PROJECT.md's core module list.

## WHY THIS MODULE IS NEEDED

- Phase 1 assumes admins manually coordinate fulfillment behind the scenes. This does not scale past a small booking volume.
- A Provider role becomes necessary once providers need to see their own jobs, accept/reject work, mark completion, and get paid without an admin doing it by hand for every booking.
- Already referenced in PROJECT.md's core module list ("Identity, Customer, Provider, Catalog...") — this document is the detailed spec for that module.

## SCOPE BOUNDARY

- This module must remain independent of the Customer, Booking, and Admin domains.
- The Booking domain should depend on Provider through exactly one bridge entity (`booking_provider_assignment`) plus one denormalized display field (`assigned_provider_id`) on `booking`.
- No other Booking logic should read Provider internals directly.
- This boundary is what keeps the module extractable into a separate service later, consistent with ARCHITECTURE.md's modular monolith principle.

## DATA MODEL

### Identity Domain

| Table | Purpose |
|---|---|
| `provider` | id, legal_name, display_name, provider_type (individual/company), phone, email, status (pending_verification / active / suspended / deactivated), onboarding_status, created_at |
| `provider_auth_identity` / `provider_session` / `provider_otp` | Auth, mirrors the customer auth tables |
| `provider_kyc_document` | doc_type, doc_number, file_ref, verification_status, verified_by, verified_at |
| `provider_address` | Base/operating address(es) |

### Capability & Coverage Domain

| Table | Purpose |
|---|---|
| `provider_skill_mapping` | provider_id → category/service they're qualified for |
| `provider_service_area` | provider_id → city/zone/pincode coverage |
| `provider_availability` | Day-of-week windows, blackout dates — feeds the existing Slot Engine |
| `provider_capacity` | Max jobs per day/slot, if capacity-based dispatch is used |

### Assignment Bridge

| Table | Purpose |
|---|---|
| `booking_provider_assignment` | booking_id, provider_id, assigned_by (admin/system), assigned_at, status (assigned/accepted/rejected/reassigned), response_deadline |

### Financial Domain

| Table | Purpose |
|---|---|
| `provider_earning_ledger` | Append-only, mirrors `wallet_ledger` — credit per completed job, debit for penalties, references booking_id |
| `provider_payout` | payout_id, provider_id, period_start/end, total_amount, status (pending/processing/paid/failed), payout_reference |

### Reputation & Ops Domain

| Table | Purpose |
|---|---|
| `provider_rating_summary` | Rolled-up average/count (raw reviews stay in the existing `review` table plus a new `provider_id` column) |
| `provider_note` | Admin-facing notes, mirrors customer notes |
| `provider_status_history` | Audit trail, mirrors `booking_status_history` |

## API SURFACE

### Provider-Facing (new `provider-api`, same pattern as `admin-api` / `consumer-api`)

- **Auth:** register, otp/send, otp/verify, login, refresh, logout
- **Profile/Onboarding:** get/update profile, upload KYC documents, get KYC status, update service areas, update skills
- **Availability:** get/update availability, set blackout dates
- **Jobs:** list jobs (filter by status/date), get job detail, accept/reject/start/complete job, mark en-route/arrived (task 270 — both optional, start stays reachable without them), report location (task 269), upload completion proof
- **Earnings:** get earnings summary, get earnings ledger, list payouts, get payout detail

### Admin-Facing Additions (extend existing `admin-api`)

- Provider CRUD: list/create/update providers, get provider detail
- KYC approval: approve/reject provider KYC
- Assignment: assign provider to a booking
- Performance: get provider performance metrics
- Payouts: run payout batch, list payouts

## RBAC ADDITIONS

Two new permission modules added to the existing matrix (SRS §20):

- **Provider** — View / Create / Edit / Approve / Suspend
- **Payout** — View / Process / Approve

## REPOSITORY PLACEMENT

```
backend/
  provider-api/              new project, same shape as admin-api/consumer-api
  shared/
    Domain/Provider/         Provider, ProviderKycDocument, ServiceArea, Availability,
                             BookingProviderAssignment, EarningLedger, Payout
    Application/Provider/    RegisterProvider, VerifyKyc, AssignProviderToBooking,
                             AcceptJob, CompleteJob, CalculatePayout
    Infrastructure/Provider/ repositories, EF configurations
```

Booking domain changes are minimal: one nullable `AssignedProviderId` field for display; no other structural change.

## OPEN DECISIONS — RESOLVED (task 144)

All five decisions below are resolved for v1. Each pick is the simplest option
that does not block extending the model later — every decision keeps the door
open for the richer option (automatic assignment, company providers, gateway
payouts, rating-weighted assignment, multi-provider bookings) without a
breaking schema change, so a future phase can extend rather than migrate.

1. **Assignment: manual (admin-driven) in v1.** An admin explicitly assigns a
   provider to a booking via `booking_provider_assignment` (task 147). No
   auto-dispatch/matching engine is built now — that requires ranking logic
   (distance, skill, capacity, rating) that doesn't exist yet and would be
   premature to guess at. The bridge table's `assigned_by` column already
   distinguishes `admin` from `system`, so an automatic assignment engine can
   be added later purely as a new writer of that same table.

2. **Provider type: always an individual in v1.** `provider_type` is modeled as
   an enum with both `Individual` and `Company` values (matching this
   document's DATA MODEL), but the domain entity's public constructor only
   accepts `Individual` for now and rejects `Company` — there is no
   sub-technician concept, roster, or company-level auth in this phase. This
   keeps the column/enum shape ready for company providers later without
   implementing the (materially larger) multi-user-per-provider auth and
   assignment model now.

3. **Payouts: manual bank transfer in v1.** `provider_payout.status`
   (pending/processing/paid/failed) and `payout_reference` are free-text/
   admin-updated rather than driven by a payment-gateway webhook — an admin
   runs a payout batch and records the bank transfer reference by hand. No
   new gateway integration is added. (Note: `provider_payout` itself is part
   of the Financial Domain, scheduled beyond task 146c — this decision
   governs its eventual implementation, not something built in this pass.)

4. **Rating does not affect assignment in v1.** `provider_rating_summary`
   exists for display (provider performance views, admin provider detail) but
   the manual assignment flow (decision 1) does not read it to rank or
   restrict candidates. Once automatic assignment exists, rating becomes a
   natural input to that ranking — deferred, not discarded.

5. **Exactly one provider per booking.** `booking_provider_assignment` models a
   single current assignment per booking (reassignment replaces it, tracked
   via the `reassigned` status rather than a second concurrent row). No
   multi-provider/crew booking support in v1.

## OPEN DECISIONS — AUTOMATIC ASSIGNMENT, RESOLVED (task 242)

Decision 1 above deferred automatic assignment explicitly, on the grounds
that "requires ranking logic (distance, skill, capacity, rating) that
doesn't exist yet and would be premature to guess at." Phase 14
(`tasks.csv` 242-250) is that engine. Manual assignment (decision 1) is
unchanged and remains available — this adds a second, automatic writer of
`booking_provider_assignment` (`assigned_by = System`) alongside the
existing admin one, never replacing it. The six decisions below are resolved
for v1, same "simplest option that doesn't block the richer one later"
approach as the five above.

1. **Distance: Haversine over plain lat/long, not PostGIS.** No
   `NetTopologySuite`/PostGIS package exists anywhere in this solution today
   (confirmed by grep across every `.csproj`), and this codebase's existing
   geo data is already plain `decimal(9,6)` lat/long columns
   (`CustomerAddress.Latitude`/`Longitude`, snapshotted onto
   `Booking.AddressLatitudeSnapshot`/`LongitudeSnapshot`) rather than a
   `geography` type — task 243 gives `Provider` the same shape. A great-circle
   distance formula is a few lines of `Math` and needs no new dependency,
   matching CODING-STANDARDS.md's dependency-only-when-justified guidance;
   revisit if candidate volume ever makes an in-process O(n) scan (task
   244's actual approach — filter by skill/area first, then rank the much
   smaller remaining set by distance) too slow for real query patterns.
   *(Superseded as the **ranking** key by task 267, which orders the
   surviving candidates by real road travel time from `IRouteEstimateProvider`
   instead — a straight line picks the wrong provider whenever a river, a
   highway or a one-way system puts the nearest-by-air candidate furthest by
   road. Haversine is still the whole story everywhere else: it pre-filters
   which candidates are worth a billed route call
   (`AutoAssignmentOptions.RouteRankingRadiusKm`/`MaxRouteCandidates`, cost
   caps only — never an eligibility filter), it orders everything not priced,
   and it is the ordering the engine falls back to when
   `AutoAssignmentOptions.RouteRankingEnabled` is off or the routing provider
   returned no real road data. Still no PostGIS, and still no new dependency
   in this solution — the routing seam was already built for booking
   tracking.)*

   One approximation is taken deliberately and stated here rather than
   buried: the provider drives **to** the customer, but the batched routing
   API takes one origin and many destinations, so the engine asks the mirror
   question — from the booking's address out to each candidate — and treats
   the answer as the inbound leg. Traffic is directional, so the two are not
   the same number. The alternative is one HTTP round trip per candidate
   instead of one per booking: the same billed element count, but N times the
   latency on a path that must not hold up a booking. The bias applies
   equally to every candidate (they share one origin), so it distorts the
   *ranking* only where one candidate's own asymmetry differs from its
   peers' — a far smaller error than the straight-line ordering it replaces.

2. **Capacity: hard-enforced for auto-assignment only, still advisory for
   manual.** `ProviderCapacity`'s doc comment states it is "advisory only in
   v1 — nothing enforces these limits automatically." Task 245 enforces
   `MaxJobsPerDay`/`MaxJobsPerSlot` when the automatic engine filters
   candidates — a machine picking a provider needs a hard cap, since there is
   no human in the loop to notice an overload the way an admin browsing a
   list would. Manual admin assignment keeps today's advisory-only behaviour
   unchanged; enforcing it there too is a separate, undecided product
   question this task does not resolve. *(Still true for the capacity
   **limits**. Decision 7 below separates out the one thing that is not a
   limit at all — being in two places at once — and makes that a hard stop on
   both paths.)*

3. **Rating: still not an input — corrected while implementing task 244.**
   Decision 4 above (task 144) left this "deferred, not discarded... once
   automatic assignment exists, rating becomes a natural input to that
   ranking," and this decision originally planned to make it the tie-break
   among candidates equally ranked by distance. Implementing task 244 found
   that premise false: `provider_rating_summary` was never built at all
   (only 4 of DATA MODEL's 5 Reputation & Ops tables exist in code —
   `provider_note`/`provider_status_history` exist, this one doesn't), and
   `ProviderPerformanceResponse`'s own doc comment already states why:
   "`provider_rating_summary` is out of this pass's scope... no
   review-to-provider link exists yet" — `Review` has no `ProviderId`
   column, so there is no query that could compute a per-provider rating
   today even ad hoc. Rating is **not used** by the matching engine in this
   pass, corrected here rather than left silently wrong; task 244 breaks an
   exact-distance tie by `Provider.Id` instead, purely for deterministic
   ordering, carrying no ranking meaning. Building a real rating input needs
   its own task (`Review.ProviderId` + a migration to backfill it from
   existing `Booking.AssignedProviderId` history) — out of scope here, not
   quietly folded in.

4. **Trigger: `AwaitingFulfilment`, after payment, not `PaymentPending`.**
   Task 240 (`tasks.csv`) established that a `PaymentPending` booking may
   never be paid for at all and now expires unassisted — reserving a
   provider's time against a booking that might vanish in 20 minutes would
   waste real dispatch capacity for nothing. The engine runs once a booking
   reaches `AwaitingFulfilment` (task 246), the same status manual admin
   assignment already targets today.

5. **No eligible candidate: unchanged manual queue, not a hard failure.** A
   booking with no provider matching skill + area + availability + capacity
   simply stays `AwaitingFulfilment` — exactly where it already sits today
   before any assignment, manual or automatic. No new status, no error
   surfaced to the customer; an admin's existing manual-assignment queue
   picks it up the same way it picks up every `AwaitingFulfilment` booking
   now. The automatic engine is purely additive — a booking it can't place
   is no worse off than before this phase existed.

6. **Rejection retry cap: 3 automatic attempts, then the manual queue.**
   Task 247 retries the matching engine (excluding every provider already
   rejected for that booking) up to 3 times before falling back to decision
   5's unchanged manual queue. Three, not unlimited: past a handful of
   declines the pattern is more likely a genuinely hard-to-place
   booking (remote address, unusual service, tight slot) than one more retry
   fixing it, and an unbounded retry loop against a small local candidate
   pool would just cycle the same providers. Configurable
   (`AutoAssignmentOptions`, task 248), not hardcoded — the number itself
   is a guess with no production data behind it yet.

7. **Time overlap: a hard invariant on every path, not a capacity limit
   (task 288).** Decision 2 treats `ProviderCapacity` as policy — hard for the
   engine, advisory for an admin. Overlapping slots are not policy: one person
   cannot be in two places at once, so an admin gets the same refusal the
   engine does (409 `BookingProviderAssignment.ProviderDoubleBooked`, naming
   the booking collided with, rather than a silent filter). Three things this
   fixed, all of which meant nothing prevented double-booking at all: a
   provider with no `provider_capacity` row (which is every provider — nothing
   in the codebase can create one) counted as *unlimited*; `MaxJobsPerSlot`
   compared slot-window **identity**, so 09:00-11:00 and 10:00-12:00 in two
   different windows never collided; and the slot time snapshots were never
   compared between bookings anywhere. The rule: same provider, same
   `slot_date`, live assignment (`Assigned`/`Accepted` only), and half-open
   overlap `NewStart < ExistingEnd && ExistingStart < NewEnd` — half-open so
   back-to-back 09:00-11:00 and 11:00-13:00 jobs stay legal. Checked inside a
   Serializable transaction and backed on PostgreSQL by an `EXCLUDE USING
   gist` constraint on `booking`; that constraint is not reproducible on the
   SQLite test provider and the divergence is documented in the migration.

## NEXT STEPS

1. ~~Resolve the open decisions above.~~ Done (task 144; automatic-assignment decisions done task 242).
2. Add table-by-table schema to DATABASE.md.
3. Add endpoint contracts to API.md.
4. Create `backend/provider-api`, mirroring the existing `admin-api`/`consumer-api` structure (task 149).
5. Extend the RBAC permission matrix and admin UI for provider management (task 150).
6. Build the automatic-assignment engine per the decisions above (tasks 243-250).

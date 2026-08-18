# AMC.md

Annual Maintenance Contract (AMC) module specification: prepaid, entitlement-based
service coverage for a specific appliance or asset.

## STATUS

**Implementation in progress**, started 2026-08-18. Domain model, migration,
application services, both API surfaces, both frontends, notification
wiring and a test suite are now built, and the redeem flow is **operable
end-to-end** since task `#331` gave a zero-payable booking its own
confirmation path in the shared payment pipeline (OPEN DECISIONS #5, now
resolved). Scoped as **Phase 20**
(`tasks.csv` `#323`–`#331`). Identified as a Critical gap in
[LAUNCH-READINESS-AUDIT.md](LAUNCH-READINESS-AUDIT.md) §4 and
[MARKET.md](MARKET.md) §5.1: Subscription and Recurring Bookings exist, but
neither models prepaid entitlement drawdown, scheduled preventive visits, or a
renewal pipeline — and MARKET.md §3 identifies AMC as having the best
cash-flow profile in the catalogue (cash arrives before cost, renewal
acquisition cost is near zero).

This document is the **specification**, not a status record.
[ORIENTATION.md](ORIENTATION.md) is the single owner of current repository
state — consult it, not this header, for what is built.

**MVP scope, stated up front.** This phase ships the core entitlement
lifecycle — plan catalog, purchase, entitlement drawdown against a real
booking, expiry, and an admin renewal-pipeline report. It deliberately does
**not** ship: automatic recurring billing for renewal (a customer renews by
purchasing again, the same one-time-payment model as the initial purchase —
auto-charge-on-renewal is future work, tracked as an open decision below,
not silently assumed done), or a scheduled-preventive-visit calendar (the
customer books each visit themselves and it draws down the entitlement; a
push-notification "your AMC visit is due" reminder is future work, same
reason). Both are named explicitly rather than left to be discovered missing
later — the discipline this whole audit exists to enforce.

## PURPOSE

An AMC is a customer paying once, upfront, for a fixed number of service
visits against one appliance or asset over a fixed term (typically 12
months) — "2 AC services this year for ₹3,499" — rather than paying per
visit. It is related to Subscription (both are a prepaid customer
commitment) and to Recurring Bookings (both produce bookings over time) but
is **structurally distinct from both**:

| | Subscription | Recurring Booking Plan | AMC |
|---|---|---|---|
| What is prepaid | A discount/benefit tier | Nothing — each occurrence is billed normally | A fixed number of visits |
| What is consumed | Free-visit *count*, resets never (fixed at subscribe time) | N/A — not a balance | Visit *entitlement*, drawn down to zero |
| Scope | Account-wide (any service) | One recurring cadence for one service | One specific appliance/asset |
| Cadence | Billing-cycle renewal (monthly/quarterly/yearly charge) | Fixed schedule (weekly/biweekly/monthly) | Customer-initiated, any time within the term, until entitlement or term runs out |
| Renewal | Auto-charge each cycle | N/A — runs until cancelled or occurrence count reached | One-time purchase again at term end |

Conflating any of these into an existing module would blur exactly the
distinction a customer and an admin both need: "how many visits do I have
left on my AC's cover" is not a question Subscription or Recurring Bookings
can answer.

## DESIGN PRINCIPLE: REUSE, DON'T DUPLICATE

Same principle as every other Phase 10+ module — a thin layer over
infrastructure that already exists:

- **An AMC visit is an ordinary `Booking`.** Redeeming entitlement calls the
  exact same `IBookingService.CreateAsync` orchestration a normal "Book now"
  tap uses (service area, slot availability, pricing engine all still run),
  the same principle `RecurringBookingPlan`'s doc comment states for
  recurring occurrences — there is no second booking-creation code path to
  keep in sync. The **price is zero** on an entitlement-covered booking
  (paid for at purchase time), the same "snapshot the discount, not a live
  join" pattern `Booking.SubscriptionDiscountAmountSnapshot` already
  establishes for Subscription-discounted bookings.
- **Purchase does NOT reuse the payment gateway pipeline** — corrected after
  implementation started. `PaymentTransaction.BookingId` is a required FK,
  and the whole gateway-order/webhook/commission/escrow machinery assumes
  every transaction belongs to a booking. Bolting a non-booking payable onto
  that entity is real architectural work of its own (see OPEN DECISIONS
  below), not something this phase does. `CustomerAmcContract.PaymentTransactionId`
  is nullable and left null for MVP; purchase is recorded, not charged.
- **The admin config pattern already exists** (Coupon, Subscription,
  Referral, Nestly Coins) — `AmcPlan` follows the same shape: an
  admin-editable catalog row, not hardcoded terms.
- **Notification wiring reuses the existing trigger framework** (SRS 19.1) —
  new `NotificationEventType` values dispatched through the same
  email/SMS/push channels, no new delivery mechanism.

## HOW IT WORKS

```
Admin publishes an AmcPlan (category, price, term months, visits included)
        │
        ▼
Customer purchases a plan for a specific asset
        │  → CustomerAmcContract created: VisitsRemaining = VisitsIncluded,
        │    Status = Active, term = [now, now + TermMonths]
        │  → payment charged via existing PaymentTransaction flow
        ▼
Customer redeems a visit (any time within the term, while VisitsRemaining > 0)
        │  → an ordinary Booking is created via IBookingService.CreateAsync,
        │    zero-priced, linked back to the contract
        │  → on booking completion, VisitsRemaining decrements by 1
        ▼
Contract reaches VisitsRemaining = 0, or EndDateUtc passes
        │  → Status → Exhausted or Expired
        ▼
Admin renewal report surfaces contracts nearing expiry / exhaustion
        │  → customer purchases a fresh plan (no auto-charge in this MVP)
```

Entitlement decrements **on booking completion, not on booking creation** —
matching Subscription's `FreeVisitsRemaining` consumption point and the
principle every other credit-consuming flow in this codebase already
follows: a cancelled-before-completion visit must not cost the customer an
entitlement.

## DATA MODEL

### New

| Table | Purpose |
|---|---|
| `amc_plan` | Admin-editable catalog: `category_id`, `name`, `description`, `price`, `term_months`, `visits_included`, `is_active`, `created_at_utc`, `updated_at_utc` |
| `customer_amc_contract` | The aggregate root. `customer_id`, `plan_id` (traceability only), plan-term snapshot fields (`plan_name_snapshot`, `category_id_snapshot`, `price_snapshot`, `term_months_snapshot`, `visits_included_snapshot` — same "snapshot at purchase time" convention as `CustomerSubscription`, so an admin editing a plan never reprices an existing contract), `asset_label` (free text, e.g. "Living room split AC" — lets one customer hold more than one contract and tell them apart), `visits_remaining`, `status` (Active / Exhausted / Expired / Cancelled), `start_date_utc`, `end_date_utc`, `payment_transaction_id`, `expiring_soon_notified_for_end_date_utc` (nullable, mirrors `CustomerSubscription.ExpiringSoonNotifiedForPeriodEndUtc` — a reminder fires once per contract, not once per report run) |
| `amc_service_visit` | Append-only, mirrors `RecurringBookingOccurrence`: `contract_id`, `booking_id`, `consumed_at_utc`. One row per redeemed visit — the contract's own audit trail, queried independently of loading the contract (same reasoning `RecurringBookingOccurrence` gives for not being a navigation collection). |

### Reuses (no new tables beyond the three above)

| Existing entity | How AMC uses it |
|---|---|
| `Booking` | Gains one nullable `AmcContractId` FK (traceability, mirrors `Booking.RecurringBookingPlanId` and `Booking.SubscriptionId`) and reuses `IBookingService.CreateAsync` unchanged for visit redemption |
| `PaymentTransaction` | Contract purchase is charged through it exactly like a one-off booking |
| `NotificationEvent` | New `NotificationEventType` values: `AmcContractPurchased`, `AmcVisitRedeemed`, `AmcContractExpiringSoon`, `AmcContractExhausted` |

## API SURFACE

### Customer-Facing (extend `consumer-api`)

- `GET /amc/plans` — active plan catalog, optionally filtered by category
- `POST /amc/contracts` — purchase a plan for a named asset (creates the
  contract; does not charge a real payment — see OPEN DECISIONS #4)
- `GET /me/amc-contracts` — the customer's contracts (active and past)
- `GET /me/amc-contracts/{id}` — one contract's detail + its visit history
- `POST /me/amc-contracts/{id}/redeem` — redeem entitlement: creates a
  zero-priced booking linked to the contract, through the existing booking
  orchestration (address, slot, service selection — same request shape as a
  normal booking, minus payment)

### Admin-Facing (extend `admin-api`)

- AMC plan catalog: list/create/update `AmcPlan` rows (mirrors
  `SubscriptionPlansController`)
- Contract list/detail: filter by status, search by customer
- Renewal report: contracts expiring or exhausted within a horizon —
  mirrors the shape `RecurringPlansController`'s report already
  established (aggregate tiles + per-record list)

## RBAC ADDITIONS

**None.** Gated behind the existing `bookings.read` / `bookings.write` and
`subscription.read` / `subscription.write` tiers, split the same way
`RecurringPlansController`'s doc comment justifies for recurring plans: an
AMC contract is a way bookings come into existence (write) and a commercial
record adjacent to Subscription (read), not a new vertical. A new
`AdminModules` entry would gate a strictly weaker view of data already
readable through `BookingsController` and `SubscriptionPlansController` —
inconvenience without a real boundary.

## REPOSITORY PLACEMENT

```
backend/
  shared/
    Domain/                    AmcPlan, CustomerAmcContract, AmcServiceVisit,
                                CustomerAmcContractStatus
    Application/Amc/           AmcContracts.cs, IAmcCustomerService,
                                IAmcAdminService, IAmcPlanRepository,
                                ICustomerAmcContractRepository
    Infrastructure/
      Services/                AmcCustomerService, AmcAdminService
      Persistence/
        Configurations/        AmcPlanConfiguration,
                                CustomerAmcContractConfiguration,
                                AmcServiceVisitConfiguration
        Repositories/           AmcPlanRepository, CustomerAmcContractRepository
  consumer-api/.../Controllers/AmcController.cs
  admin-api/.../Controllers/AmcPlansController.cs, AmcContractsController.cs
  admin-web/.../amc/          plan catalog, contract list, renewal report
  customer-web/.../amc/       plan browse, purchase, "my AMC" list, redeem flow
database/migrations/          AddAmcSchema
```

## OPEN DECISIONS

1. **Auto-renewal billing is out of MVP scope.** A contract simply expires;
   the customer purchases a fresh plan the same way they bought the first
   one. Auto-charge-on-renewal (Subscription's retry-with-backoff pattern,
   `CustomerSubscription.RecordFailedCharge`) is the natural next step once
   real usage data shows renewal-purchase drop-off is a problem worth
   solving with more billing machinery.
2. **Scheduled preventive-visit reminders are out of MVP scope.** The
   customer redeems visits whenever they choose within the term; there is no
   "your 6-month checkup is due" push. `AmcContractExpiringSoon` covers term
   expiry only, not a mid-term cadence reminder. If usage data shows
   customers under-redeem and let entitlement lapse unused, a scheduled
   reminder job (mirroring `IRecurringBookingSchedulerService`) is the fix.
3. **One asset per contract.** A customer with two ACs buys two contracts.
   Multi-asset contracts (one contract, N assets, shared entitlement pool)
   were considered and rejected for MVP — it complicates the redemption flow
   ("which asset is this visit for") for a case the Jaipur launch does not
   need on day one.
4. **Purchase does not charge a real payment.** Found mid-implementation,
   not anticipated at design time: `PaymentTransaction.BookingId` is a
   required foreign key, and the gateway-order/webhook/commission/escrow
   pipeline it feeds assumes every transaction belongs to a booking. Making
   that pipeline accept a non-booking payable — an AMC contract — is a real
   architectural change (touches `PaymentService`, `PaymentWebhookService`,
   `PaymentTransactionConfiguration`, and every downstream commission/escrow
   consumer), bigger than this phase's scope. `CustomerAmcContract.PaymentTransactionId`
   is nullable and left null; `PurchaseAsync` records the contract without
   charging anything. This is the honest MVP boundary, not a bug to be
   silently worked around — real gateway integration for AMC purchase is
   follow-up work, same status as decisions #1 and #2 above.
5. **RESOLVED (task `#331`) — the redeem flow now completes end-to-end.**
   Found during implementation, not before: `BookingService.CreateAsync`
   used to transition *every* new booking to `PaymentPending`, and
   `PaymentTransaction`'s constructor rejects a non-positive `amount` — so a
   zero-priced redemption booking had no way to leave `PaymentPending`
   through the existing payment pipeline (`PaymentService.CreateOrderAsync`
   → `PaymentWebhookService` → `Confirmed`) and was created correctly but
   stuck forever. Fixed in the shared pipeline rather than narrowly for AMC,
   because AMC was never the only producer of a zero payable: **a booking
   with nothing left to pay is now confirmed on creation**
   (`Initiated → Confirmed`, a new edge in `BookingLifecycle`, carrying the
   reason "Nothing payable on this booking - confirmed without a payment" in
   the booking's own status timeline). The rule is stated as "is anything
   payable", so it equally covers a fully wallet-covered checkout (verified
   reachable: `BookingSummaryService` applies wallet credit capped at what
   remains payable, which can be the whole of it) and a coupon/subscription
   discount that takes the total to zero. No zero-amount `PaymentTransaction`
   is fabricated: the audit answer to "how was this booking settled" is the
   booking's own zero `FinalPayable` plus the record of what covered it —
   `AmcContractId`, the wallet ledger entry, or the coupon snapshot.
   `EscrowReleaseOnCompletionHandler` treats a never-charged booking as a
   silent no-op (no escrow was ever held) instead of a data-integrity
   warning.

## NEXT STEPS

Backlog: `tasks.csv` `#323`–`#331`, Phase 20.

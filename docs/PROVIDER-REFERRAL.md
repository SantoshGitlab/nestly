# PROVIDER-REFERRAL.md

Provider Referral (provider-refers-provider) module specification.

## STATUS

**Implemented.** Delivered in one pass on `feature/provider-referral-program`
(merged to `main` at `cdde3d3`), task row #392 in `tasks.csv`. Referral codes,
qualifying-job vesting, reward disbursement, program config, manual fraud
review, and both the provider-facing Refer & Earn screen and the admin
config/list/detail/fraud-review screens are live.

This document is the **specification**, not a status record.
[ORIENTATION.md](ORIENTATION.md) is the single owner of current repository
state — consult it, not this header, for what is built.

## PURPOSE

Provider supply, not customer demand, is the harder bootstrap problem for a
new-city marketplace launch (MARKET.md's Jaipur thesis) — this module gives
existing providers a direct incentive to bring on new ones. A provider who
refers another provider earns a reward once the referee proves out as a
genuine, active worker; the referee earns a welcome bonus for reaching that
same bar. This document defines the **ProviderReferral** module: how a code
is generated and shared, what "qualifies" a referral, how the reward is paid,
and how it plugs into the provider earning ledger and booking lifecycle
without duplicating either.

## RELATIONSHIP TO REFERRAL.md

This module is a structural mirror of the customer-side
[REFERRAL.md](REFERRAL.md) — same lifecycle shape, same fraud-review
mechanism, same admin/self-service split — adapted for the supply side. It is
a **separate module**, not an extension of REFERRAL.md's `Referral` entity:
the two programs reward opposite sides of the marketplace, have independent
config/fraud-review surfaces, and are owned by different admin roles
(Operations Admin here, Marketing Admin there — see RBAC ADDITIONS below).

Two deliberate differences from the customer program, both driven by what
actually differs between a customer and a provider:

1. **Reward channel: earning-ledger credit only, no coupon option.** A
   coupon is a customer-facing discount instrument (SRS 11.10) with no
   equivalent meaning for a provider's own earnings — there is nothing to
   discount. Every reward, both sides, credits
   `ProviderEarningLedgerEntry` via a new
   `ProviderEarningSourceType.ProviderReferralReward`, mirroring how
   `WalletSourceType.ReferralReward` works for customers.
2. **Qualification: N completed jobs, not one booking's amount.** A provider
   referral pays out real money for bringing on a new *worker*, not for a
   single transaction. A single completed job is a weak signal that the
   referee is a genuine, active provider rather than a fabricated account
   created only to trigger a signup reward — requiring several completed
   jobs before either side is paid is this module's primary fraud control,
   in place of the customer program's minimum-order-amount threshold.

## HOW IT WORKS

```
Referrer shares their code/link
        │
        ▼
Referee registers using the code (either mobile-OTP or email-OTP flow)
        →  ProviderReferral row created (status: Registered)
        │
        ▼
Referee's assigned bookings reach Completed, one at a time
        │
        ▼
Referee's completed-job count reaches the configured threshold
        →  ProviderReferral marked Qualified  →  reward credited to both
           sides' earning ledgers  →  marked Rewarded
```

A referral that never reaches the qualifying job count within the configured
expiry window is marked **Expired** by a daily sweep — no reward, no error,
just a closed row.

## DATA MODEL

### ProviderReferral Domain

| Table | Purpose |
|---|---|
| `provider_referral` | id, referrer_provider_id, referee_provider_id, referral_code_used, status (registered / qualified / rewarded / expired), qualifying_booking_id (nullable), referrer/referee_reward_value (snapshot), qualifying_completed_jobs_count (snapshot), referrer/referee_earning_entry_id (nullable), registered_at_utc, qualified_at_utc, rewarded_at_utc, expires_at_utc, is_fraud_flagged, fraud_review_note, fraud_reviewed_by_admin_user_id, fraud_reviewed_at_utc |
| `provider_referral_program_config` | Admin-editable, single mutable row: referrer_reward_value, referee_reward_value, qualifying_completed_jobs_count, referral_expiry_days, max_referrals_per_provider (fraud cap, nullable = unlimited), is_active, updated_at_utc, updated_by_admin_user_id |

Reward terms are snapshotted onto `ProviderReferral` at registration time
(same non-retroactivity reasoning as `ReferralProgramConfig`'s own doc
comment) — a later admin config change can never retroactively alter a
reward already promised to an in-flight referral.

### Reuses (no new tables beyond the two above)

| Existing entity | How provider referral uses it |
|---|---|
| `Provider` | Gains one nullable `ReferralCode` field + a `SetReferralCode()` method, generated lazily on first request — mirrors `Customer.ReferralCode` exactly |
| `ProviderEarningLedgerEntry` | New `ProviderEarningSourceType.ProviderReferralReward` value; credited via the existing `IProviderEarningLedgerService.RecordAdjustmentAsync` |
| `Booking` | Read-only trigger point: the same `BookingStatusChangedEvent` the customer referral handler already subscribes to, read here for `AssignedProviderId` and a completed-job count instead of `CustomerId` and an order amount |

## FRAUD / ABUSE PREVENTION

Same posture as REFERRAL.md's "this is a direct cash-cost surface" framing,
adapted:

- **Self-referral block**: referrer and referee must not resolve to the same
  person by phone or email (checked at registration). In practice this is
  unreachable through the public flow today for the same reason REFERRAL.md
  documents for customers: `Provider.Phone`/`Email` are unconditionally
  DB-unique, so a genuine attempt fails earlier on
  `ProviderRegistration.MobileAlreadyRegistered` — this check is defense in
  depth against that constraint being relaxed later, not currently
  exercisable code (see `ProviderReferralRegistrationTests` for the test that
  documents this finding rather than pretending otherwise).
- **One referral per referee, ever**: unique constraint on
  `provider_referral.referee_provider_id`.
- **Per-referrer reward cap**: `max_referrals_per_provider` on the config,
  admin-configurable, unlimited by default.
- **Vesting over multiple completed jobs** (see RELATIONSHIP TO REFERRAL.md
  above) is this module's structural answer to the "throwaway account for
  the signup bonus" attack that the customer program closes a different way
  (rewarding both sides on the referee's *own* qualifying event, never on
  registration alone).
- **Manual review queue, not auto-block**: a referral can be flagged for
  fraud review independent of its lifecycle status (a `Rewarded` referral
  can still be flagged after the fact). Approving a flag never auto-reverses
  an earning-ledger credit — the ledger is append-only; any actual clawback
  is a separate, deliberate admin adjustment through the existing earning
  ledger tooling, same reasoning as `Referral.IsFraudFlagged`'s doc comment.

## API SURFACE

### Provider-Facing (`provider-api`)

- `GET /api/v1/referral` — code, shareable link, lifetime stats (invited /
  qualified / rewarded / total earned)
- `GET /api/v1/referral/history` — this provider's own referrals as
  referrer, newest first
- `POST /api/v1/auth/registration` and `.../registration/email` — both
  accept an optional `referralCode`

### Admin-Facing (`admin-api`)

- `GET/PUT /api/v1/admin/provider-referral/config` — the single program
  config row
- `GET /api/v1/admin/provider-referral` — filterable, paginated list
- `GET /api/v1/admin/provider-referral/fraud-queue` — flagged rows only
- `GET /api/v1/admin/provider-referral/{id}` — detail
- `POST .../{id}/flag`, `.../{id}/approve`, `.../{id}/reject` — fraud review
  actions

## RBAC ADDITIONS

One new permission module, `AdminModules.ProviderReferral`
(`"provider-referral"`), collapsed to the existing two tiers (Read/Write)
same as `Referral`/`Chat`/`NestlyCoins` before it — see
`AdminModules.Referral`'s doc comment for the full reasoning, unchanged here.
Auto-seeded at startup by `AdminPermissionReconciler` (no dedicated
`SeedXPermissions` migration needed — see that class's doc comment for why).

Granted **write** to Operations Admin (this is supply-side operational work,
the same tier as the `Provider` module it already owns) and **read** to
Finance Admin (cost visibility only). Deliberately *not* granted to
Marketing Admin, unlike the customer `Referral` module — this program is
about acquiring and vetting providers, not a customer-facing growth
campaign.

## NOTIFICATION EVENTS

**None.** `NotificationEvent.CustomerId` is a required foreign key to the
customer table, so it cannot record a provider actor without a schema
change — the same constraint `CustomerRegistrationService`'s doc comment
already notes blocks a provider welcome notification. A referrer sees a new
invite, and both sides see a reward land, only via
`GET /api/v1/referral/history` (in-app, not push/email/SMS). Extending
`NotificationEvent` to support a provider recipient is out of scope for this
module and would need its own design pass.

## REPOSITORY PLACEMENT

```
backend/
  shared/
    Domain/                          ProviderReferral, ProviderReferralProgramConfig,
                                      ProviderReferralStatus; Provider.ReferralCode
    Application/ProviderReferral/    contracts, validators, service/repository interfaces
    Infrastructure/
      Persistence/                   ProviderReferralRepository, ProviderReferralProgramConfigRepository,
                                      EF configurations
      Services/                      ProviderReferralCodeService, ProviderReferralProviderService,
                                      ProviderReferralRewardService, ProviderReferralAdminService,
                                      ProviderReferralProgramConfigAdminService,
                                      ProviderReferralFraudReviewService,
                                      ProviderReferralQualifyingJobHandler (MediatR),
                                      ProviderReferralExpirySweepService
      BackgroundJobs/                ProviderReferralExpirySweepJobScheduleExtensions (Hangfire, daily)
  provider-api/.../Controllers/      ReferralController.cs (self-service)
  admin-api/.../Controllers/         ProviderReferralsController.cs, ProviderReferralProgramConfigController.cs
  provider-web/.../refer-earn/       self-service screen, linked from Profile
  admin-web/.../provider-referral/   config, list/fraud-queue, detail screens
```

Booking domain changes: **none structural.** The completion path gains one
read of `IProviderReferralRepository` to check for a pending referral keyed
by the assigned provider, the same shape as the customer-side handler and
the existing notification-trigger checks.

## DECISIONS

1. **Qualification is the referee's own completed-job count, not the
   referrer's.** Only the referee proving out as an active provider unlocks
   either side's reward — mirrors REFERRAL.md's own "closes the throwaway
   account loophole" reasoning, applied to the supply side's actual risk
   (a fabricated *worker* account, not a fabricated *order*).
2. **No coupon reward option.** Considered and rejected — see RELATIONSHIP
   TO REFERRAL.md above.
3. **No notification dispatch.** Considered and rejected for this pass —
   see NOTIFICATION EVENTS above. Both sides can still see their referral
   progress and reward via the self-service history endpoint.

## FUTURE ENHANCEMENTS (not queued — explicit v1 trims)

Mirroring REFERRAL.md's own phased delivery, the following are deliberately
**not** part of this v1, each because it depends on the base loop existing
and proving out first, not because it was overlooked:

- **Funnel and cost reports** (`ReferralsController`'s `reports/funnel` and
  `reports/cost` equivalents). The admin list/detail screens already answer
  "what happened to this referral"; a programme-wide report is a distinct
  deliverable REFERRAL.md itself only added as task #171, after the base
  loop (#161-166) shipped.
- **Milestone bonus tiers** (a bonus on top of the per-referral reward past
  a referrer's Nth qualified referral) — REFERRAL.md's own task #174, queued
  after its base loop, not before.
- **Provider-side notification dispatch** — see NOTIFICATION EVENTS above;
  needs a `NotificationEvent` schema change, a separate design pass.
- **Expiring earning-ledger credit** — REFERRAL.md's task #175 flags this as
  "not a small addition" for the customer wallet and explicitly defers it
  until FIFO consumption-tracking is designed; the same reasoning applies
  unchanged to the provider earning ledger, which has the identical
  single-running-balance shape.

**Explicitly not queued**: a public referrer leaderboard, same reasoning as
REFERRAL.md's identical exclusion — a gamification feature that incentivizes
exactly the fake-account behavior the fraud review queue exists to catch.

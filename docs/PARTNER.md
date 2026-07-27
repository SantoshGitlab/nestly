# PARTNER.md

Partner / Vendor module specification.

## STATUS

Not implemented. Out of scope for Phase 1 per the SRS (§4.2 Excluded Direct End-User Interfaces, §34 Open Decision #9). This document defines the target design now so that Booking, Database, and API decisions made elsewhere don't accidentally block adding this module later.

## PURPOSE

Nestly connects customers to services, but a person must actually fulfill each booking. This document defines the **Partner** (service provider) module: identity, availability, assignment, earnings, and how it integrates with the existing Customer/Booking/Admin domains without breaking module boundaries.

Note on terminology: the SRS uses "vendor" only to mean external third-party providers (payment gateway, SMS/Email/WhatsApp). The platform role described here — the person or company who fulfills a booking — is called **Partner**, matching the module name already listed in PROJECT.md's core module list.

## WHY THIS MODULE IS NEEDED

- Phase 1 assumes admins manually coordinate fulfillment behind the scenes. This does not scale past a small booking volume.
- A Partner role becomes necessary once partners need to see their own jobs, accept/reject work, mark completion, and get paid without an admin doing it by hand for every booking.
- Already referenced in PROJECT.md's core module list ("Identity, Customer, Partner, Catalog...") — this document is the detailed spec for that module.

## SCOPE BOUNDARY

- This module must remain independent of the Customer, Booking, and Admin domains.
- The Booking domain should depend on Partner through exactly one bridge entity (`booking_partner_assignment`) plus one denormalized display field (`assigned_partner_id`) on `booking`.
- No other Booking logic should read Partner internals directly.
- This boundary is what keeps the module extractable into a separate service later, consistent with ARCHITECTURE.md's modular monolith principle.

## DATA MODEL

### Identity Domain

| Table | Purpose |
|---|---|
| `partner` | id, legal_name, display_name, partner_type (individual/company), phone, email, status (pending_verification / active / suspended / deactivated), onboarding_status, created_at |
| `partner_auth_identity` / `partner_session` / `partner_otp` | Auth, mirrors the customer auth tables |
| `partner_kyc_document` | doc_type, doc_number, file_ref, verification_status, verified_by, verified_at |
| `partner_address` | Base/operating address(es) |

### Capability & Coverage Domain

| Table | Purpose |
|---|---|
| `partner_skill_mapping` | partner_id → category/service they're qualified for |
| `partner_service_area` | partner_id → city/zone/pincode coverage |
| `partner_availability` | Day-of-week windows, blackout dates — feeds the existing Slot Engine |
| `partner_capacity` | Max jobs per day/slot, if capacity-based dispatch is used |

### Assignment Bridge

| Table | Purpose |
|---|---|
| `booking_partner_assignment` | booking_id, partner_id, assigned_by (admin/system), assigned_at, status (assigned/accepted/rejected/reassigned), response_deadline |

### Financial Domain

| Table | Purpose |
|---|---|
| `partner_earning_ledger` | Append-only, mirrors `wallet_ledger` — credit per completed job, debit for penalties, references booking_id |
| `partner_payout` | payout_id, partner_id, period_start/end, total_amount, status (pending/processing/paid/failed), payout_reference |

### Reputation & Ops Domain

| Table | Purpose |
|---|---|
| `partner_rating_summary` | Rolled-up average/count (raw reviews stay in the existing `review` table plus a new `partner_id` column) |
| `partner_note` | Admin-facing notes, mirrors customer notes |
| `partner_status_history` | Audit trail, mirrors `booking_status_history` |

## API SURFACE

### Partner-Facing (new `partner-api`, same pattern as `admin-api` / `consumer-api`)

- **Auth:** register, otp/send, otp/verify, login, refresh, logout
- **Profile/Onboarding:** get/update profile, upload KYC documents, get KYC status, update service areas, update skills
- **Availability:** get/update availability, set blackout dates
- **Jobs:** list jobs (filter by status/date), get job detail, accept/reject/start/complete job, upload completion proof
- **Earnings:** get earnings summary, get earnings ledger, list payouts, get payout detail

### Admin-Facing Additions (extend existing `admin-api`)

- Partner CRUD: list/create/update partners, get partner detail
- KYC approval: approve/reject partner KYC
- Assignment: assign partner to a booking
- Performance: get partner performance metrics
- Payouts: run payout batch, list payouts

## RBAC ADDITIONS

Two new permission modules added to the existing matrix (SRS §20):

- **Partner** — View / Create / Edit / Approve / Suspend
- **Payout** — View / Process / Approve

## REPOSITORY PLACEMENT

```
backend/
  partner-api/              new project, same shape as admin-api/consumer-api
  shared/
    Domain/Partner/         Partner, PartnerKycDocument, ServiceArea, Availability,
                             BookingPartnerAssignment, EarningLedger, Payout
    Application/Partner/    RegisterPartner, VerifyKyc, AssignPartnerToBooking,
                             AcceptJob, CompleteJob, CalculatePayout
    Infrastructure/Partner/ repositories, EF configurations
```

Booking domain changes are minimal: one nullable `AssignedPartnerId` field for display; no other structural change.

## OPEN DECISIONS

These must be resolved before this module moves from documented to implemented:

1. Is partner assignment manual (admin-driven) or automatic (system-driven) in the first release?
2. Is a partner always an individual, or can it be a company with sub-technicians?
3. Are payouts gateway-initiated or manual bank transfer in Phase 1?
4. Does partner rating affect assignment priority?
5. Is multiple partners per booking supported, or exactly one partner per booking?

## NEXT STEPS

When this module is greenlit for implementation:

1. Resolve the open decisions above.
2. Add table-by-table schema to DATABASE.md.
3. Add endpoint contracts to API.md.
4. Create `backend/partner-api`, mirroring the existing `admin-api`/`consumer-api` structure.
5. Extend the RBAC permission matrix and admin UI for partner management.

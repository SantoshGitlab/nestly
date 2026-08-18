# GST.md

GST (Goods and Services Tax) and contracting posture recommendation: whether
Nestly transacts as agent or principal, and what that changes.

## STATUS

**Recommendation, not policy.** This document frames the decision and its
downstream consequences. It is not a tax filing position, not legal or
chartered-accountant advice, and settling it requires sign-off from
whoever owns tax/legal for the business — likely a CA or tax counsel
engagement this document cannot substitute for. Nothing here should be
treated as Nestly's actual GST registration or invoicing basis until a
qualified professional confirms it.

Written in response to [MARKET.md](MARKET.md) §5.2's "GST and contracting
posture undecided" gap (High: *"Whether Nestly transacts as agent
(commission) or principal (resells the service) changes GST treatment,
invoice format and contract structure. B2B customers cannot be invoiced
until this is settled."*).

This document does not own pricing mechanics or the revenue-model thesis —
[PRICING.md](PRICING.md) and [MARKET.md](MARKET.md) keep those. It owns the
tax-treatment *question* and what answering it would require of the
platform.

## 1. THE TWO MODELS

**Agent model (commission).** Nestly is a marketplace connecting customer
and technician; the technician performs and invoices the service, Nestly
charges a commission/platform fee for the connection. GST applies to
Nestly's commission, not the full service value. This is closer to how a
pure marketplace (a listings/lead-gen platform) is typically structured,
and closer to what Urban Company's original positioning implied, though
per MARKET.md §2.1 their current scale and vertical integration make their
actual tax structure a poor assumption to import without checking.

**Principal model (reseller).** Nestly contracts with the customer to
deliver the service, and separately contracts with the technician to
perform it — Nestly invoices the customer for the full service value and
GST applies to that full value, with the technician's payout being a
separate cost line, not a pass-through. This is closer to how a
facility-management or staffing company is typically structured, and is
what B2B/society contracts (MARKET.md §4 move #1, §5.2's B2B gap) most
naturally look like: a society signs one contract for "all common-area
maintenance," not a marketplace connection to individually-dispatched
technicians.

**Why this isn't free to leave undecided:** the two models produce
different invoices, different tax liability, and arguably different legal
relationships with technicians (bearing on [SUPPLY.md](SUPPLY.md)'s
engagement-structure question and [INSURANCE.md](INSURANCE.md)'s
coverage-product question) — three open decisions across three documents
that all converge on this one.

## 2. WHY B2B FORCES THE ANSWER FIRST

A consumer one-off booking can currently launch without this being fully
settled — `CityPricingPolicy.TaxPercentage` and the settings-page tax
configuration (`taxInclusivePricing`, `taxRegistrationNumber`) already
support showing a tax-inclusive consumer price and charging a flat rate,
which is defensible under either model for a small transaction. **A B2B
contract cannot**: a society or business customer needs a GST-compliant
invoice with a specific structure (their own accountant will check it), and
that structure is exactly what differs between agent and principal. Per
MARKET.md §5.2, *"B2B customers cannot be invoiced until this is settled"*
— this is why MARKET.md's own gap register pairs it directly with "No B2B
account model," and why this decision has to land before the B2B account
model ([PROJECT.md](PROJECT.md)/MARKET.md's largest named architectural
gap) is designed, not after.

## 3. WHAT ALREADY EXISTS

| Existing mechanism | What it does | Relevance |
|---|---|---|
| `CityPricingPolicy.TaxPercentage` | Per-city GST rate configured by admin | Works under either model for a simple flat-rate consumer charge |
| Settings `TaxSettings` (`defaultTaxPercentage`, `taxRegistrationNumber`, `taxInclusivePricing`) | Platform-wide tax display configuration | `taxRegistrationNumber` (GSTIN) is currently a single platform-wide value — correct under the principal model (Nestly is the invoicing entity); under the agent model, a *technician's* GSTIN may need to appear on the invoice instead or in addition, which is not currently modeled — see §4 |
| `PaymentTransaction` | Records the charge | Neither model changes how a payment is captured; both change what the resulting invoice document says |

**No invoice-generation, GSTIN-per-provider, or HSN/SAC-code modeling
exists in the codebase today** (confirmed absent, same verification
discipline [AMC.md](AMC.md) and [LAUNCH-READINESS-AUDIT.md](LAUNCH-READINESS-AUDIT.md)
apply elsewhere in this suite) — consistent with MARKET.md's assessment
that this is genuinely undecided, not decided-but-undocumented.

## 4. WHAT EACH MODEL WOULD REQUIRE TO BUILD

Not committing to either — naming the engineering shape so whichever is
chosen isn't designed blind:

**If agent (commission):**
- Each active technician needs a recorded GSTIN (or a documented exemption
  — many individual technicians fall under GST's small-supplier threshold,
  which is itself a fact pattern a tax professional needs to confirm, not
  an assumption to code against).
- The customer-facing invoice is generated *for* the technician's supply,
  with Nestly's commission shown as a separate line/document — a
  materially different invoice-generation surface than a single Nestly
  invoice.
- HSN/SAC service codes are per-category and may vary by what the
  technician is registered to supply, not just by Nestly's service catalog.

**If principal (reseller):**
- Nestly's own GSTIN is what appears on every invoice — simpler
  invoice-generation surface (one issuing entity), but Nestly bears full
  GST liability on gross transaction value, not just commission — a
  materially different tax cost, which the unit-economics model (§5) needs
  to price in.
- Technician payout becomes a cost line Nestly pays and may itself attract
  GST treatment on the *technician's* side depending on their registration
  status — the reverse of the agent model's structure.
- A B2B contract invoice (society, business) is a single Nestly-issued
  document for the full contract value — the natural shape for a facility-
  management-style engagement, and the more common structure for that
  segment of business generally, but that observation is not a substitute
  for the professional confirmation this document defers to.

## 5. UNIT ECONOMICS DEPENDENCY

Same dependency every other business-gap document in this suite states: the
tax cost difference between the two models (commission-only GST vs.
gross-value GST) is a real margin difference the costed unit-economics
model (MARKET.md §6 step 2) needs to reflect once a model is chosen — this
document names the difference exists, it does not quantify it.

## OPEN DECISIONS (need CA/tax counsel + business sign-off)

1. **Agent vs. principal**, the central question — likely the single
   highest-leverage decision this document can force to the top of a
   priority list, since it gates B2B invoicing, technician engagement
   structure ([SUPPLY.md](SUPPLY.md)), and the unit-economics tax-cost
   line all at once.
2. **Technician GST registration status and threshold handling**, if agent
   is chosen — whether individual technicians are expected to register, and
   how sub-threshold technicians are handled on an invoice.
3. **Whether the answer differs between consumer and B2B transactions.**
   It is possible (and worth a professional's explicit confirmation, not
   an assumption) that Nestly could run agent-model for consumer one-off
   jobs and principal-model for B2B contracts simultaneously — two
   different invoice paths for two different customer segments — rather
   than forcing one answer platform-wide.

## NEXT STEPS

Not a `tasks.csv` phase — this is a recommendation awaiting a qualified
professional's determination, per this document's own STATUS. Once settled:

1. CA/tax counsel confirms the model (or the split described in OPEN
   DECISIONS #3) against actual Indian GST law as it applies to Nestly's
   specific structure.
2. Unit-economics model reflects the resulting tax-cost line.
3. Invoice-generation and (if agent model) per-technician GSTIN tracking
   get scoped as their own engineering phase — sized properly once the
   model is known, feeding directly into the B2B account model MARKET.md
   §6 step 3 names as the next architectural priority.

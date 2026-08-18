# INSURANCE.md

Liability and insurance posture recommendation: what happens when a
technician damages a customer's property, or is injured on a job.

## STATUS

**Recommendation, not policy.** This document proposes a liability posture
and names the mechanics it would need. It is not an insurance program, not
a legal coverage determination, and not a claim on any actual policy Nestly
holds or intends to hold. Insurance underwriting, policy selection and
claim-handling terms are legal and financial decisions requiring sign-off
from legal counsel and whoever owns risk for the Jaipur launch — this
document frames the decision, it does not make it.

Written in response to [MARKET.md](MARKET.md) §5.2's "No liability /
insurance model" gap (High: *"Urban Company covers damage. One broken
marble floor without cover is an existential trust event for a new brand,
and it is the first question a society secretary asks."*).

This document does not own competitive positioning or the trust-signal
thesis behind *why* coverage matters — [MARKET.md](MARKET.md) keeps that.
It owns the coverage *shape*: what's covered, what isn't, how a claim moves
through the system, and what it would take to build.

## 1. WHY THIS IS UNAVOIDABLE, NOT OPTIONAL

Per MARKET.md §5.2, this is the literal first question a society secretary
asks before signing move #1's building contract (MARKET.md §4), and per
§2.1, Urban Company already has damage coverage as a stated capability — so
"we don't cover that" is not a neutral gap, it is a **negative** relative to
the incumbent on the exact question that gates the highest-margin revenue
line in the catalogue (society contracts, MARKET.md §3). A B2B buyer will
not sign a facility-services contract with a vendor that cannot answer this
question with a number and a process.

## 2. WHAT NEEDS COVERAGE (two different risks, two different answers)

1. **Property damage during a job** — a technician cracks a tile, scratches
   a countertop, breaks an appliance while servicing it. This is the risk
   MARKET.md §5.2 names directly and the one a society secretary asks
   about first.
2. **Technician injury on a job** — a fall, an electrical injury, a cut.
   Separate from property damage: this is a labour/workers'-comp-shaped
   liability, not a customer-facing one, but it is real exposure the moment
   Nestly has technicians (even as independent contractors — engagement
   structure affects which insurance product applies, see OPEN DECISIONS)
   working in customers' homes.

These need different insurance products and different claim workflows.
Conflating them into one "insurance" line item would hide that the
customer-facing promise (§3) and the technician-facing obligation (§4) are
not the same coverage.

## 3. PROPERTY DAMAGE: THE CUSTOMER-FACING PROMISE

Proposed shape, mirroring [PRICING.md](PRICING.md)'s quote-guarantee in
structure (a real financial commitment with a bounded ceiling, not an
unlimited promise):

1. **Scope.** Damage directly caused by the technician's work, to the asset
   or immediate work area being serviced — not pre-existing damage, not
   unrelated property. `BookingCompletionProof`'s existing before/after
   photo requirement (per PROVIDER.md and MARKET.md §4 move #6) is the
   evidentiary basis for "was this damage caused by this job" — reused, not
   duplicated.
2. **Claim path.** Customer reports damage (through support, per SRS's
   existing support-ticket flow) within a defined window after job
   completion. Claim references the booking and its completion proof.
   Admin reviews (photo evidence, technician account, claim amount) and
   approves/denies/partially-approves — the same review shape
   `RefundService` already uses for booking refunds ([API.md](API.md)'s
   existing refund endpoints), reused for a different trigger.
3. **Payout source.** Two structurally different options, and this is the
   central open decision (§5):
   - **Self-insured**: Nestly pays approved claims from its own margin,
     capped per-incident and per-period. Faster to launch (no underwriting
     lead time), but the exposure is real and uncapped in aggregate until a
     policy limit is chosen.
   - **Third-party policy**: Nestly carries an actual commercial general
     liability / property-damage policy, and approved claims are filed
     against it. Slower to stand up (insurer selection, underwriting,
     premium), but caps Nestly's own exposure and is the credible answer
     to a society secretary's question — an actual policy number, not a
     promise.

## 4. TECHNICIAN INJURY: THE OBLIGATION

This is the less negotiable of the two — worker injury exposure exists
regardless of whether it's ever formally insured, and mishandling it is
both a legal and a reputational risk (SUPPLY.md's entire recruitment thesis
depends on treating technicians *better* than the incumbent, not worse).
Proposed shape:

1. **Coverage type depends on engagement structure** (independent
   contractor vs. employee — see [SUPPLY.md](SUPPLY.md) and OPEN DECISIONS
   below): a personal accident policy for contractors is a materially
   different product and cost than statutory workers' compensation for
   employees. This decision is upstream of insurance and belongs to
   SUPPLY.md's engagement-model question, not this document.
2. **Minimum viable version**: a group personal accident policy covering
   all active technicians, sized per-technician at a cost the unit-economics
   model (§6) treats as part of technician cost-to-serve, not an
   afterthought.

## 5. WHAT THIS DOES NOT REQUIRE (reuse, don't duplicate)

No new core domain model is needed to *record* a claim — the mechanics
already exist and a claim reuses them the same way an AMC visit reuses
`IBookingService.CreateAsync` ([AMC.md](AMC.md)'s stated principle):

| Existing mechanism | Reused for |
|---|---|
| `BookingCompletionProof` | Evidentiary basis for a damage claim |
| Support ticket flow (SRS) | Claim intake |
| Admin refund review pattern | Claim approval/denial workflow shape |
| `CityPricingPolicy` / settings pattern | Where a per-city or per-category claim cap would live, if self-insured |

What **is** new, if this proceeds: a `DamageClaim`-shaped entity linking a
booking, its completion proof, a claimed amount, and a status
(Submitted/Approved/Denied/Paid) — small, and deliberately not designed in
detail here, because whether it's needed at all depends on §3's
self-insured-vs-policy decision first. Designing the schema before that
decision is made would be guessing at requirements, which this document's
own house rules (CLAUDE.md) forbid.

## 6. UNIT ECONOMICS DEPENDENCY

Same dependency [PRICING.md](PRICING.md) and [SUPPLY.md](SUPPLY.md) state:
whether self-insurance is solvent, what a third-party policy premium would
cost, and what a per-claim cap should be are all numbers the costed
unit-economics model (MARKET.md §6 step 2) needs to check, not figures this
document invents.

## OPEN DECISIONS (need legal/insurance/business sign-off)

1. **Self-insured vs. third-party policy** (§3) — the central decision,
   gating everything else including whether a `DamageClaim` entity is even
   the right shape.
2. **Technician engagement structure** (independent contractor vs.
   employee) — determines which injury-coverage product applies (§4) and
   is a legal-structure decision that also affects GST treatment (see
   [GST.md](GST.md)) and labour-law obligations outside either document's
   scope.
3. **Per-incident and aggregate claim caps**, if self-insured — needs a
   number from the unit-economics model plus a risk-tolerance call.
4. **Whether coverage is a headline marketing claim or a quiet operational
   backstop.** Urban Company states it publicly as a feature (MARKET.md
   §2.1); whether Nestly leads with it the same way, understates it until
   claim volume is understood, or restricts the public claim to B2B
   contracts specifically (where it's actually being asked for, per §1) is
   a go-to-market call for whoever owns that decision, not an engineering
   one.

## NEXT STEPS

Not a `tasks.csv` phase — this is a recommendation awaiting sign-off, per
this document's own STATUS. Once approved:

1. Legal/insurance sign-off on §3's self-insured-vs-policy decision and
   §4's technician coverage approach.
2. Unit-economics model sizes the actual cost (premium or claim-cap budget)
   against the margin thesis.
3. If self-insured or policy-backed-with-Nestly-side-tracking, scope the
   `DamageClaim` mechanics (§5) as its own phase once the shape is actually
   decided, not before.

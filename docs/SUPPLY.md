# SUPPLY.md

Technician supply acquisition plan for the Jaipur launch: how the first
100–500 providers get recruited, verified, equipped and retained.

## STATUS

**Recommendation, not policy.** This is an operational plan proposal, not an
executed program. Recruiting, vetting and paying real technicians is a
people-and-money decision outside engineering's authority — it needs
sign-off from whoever owns Jaipur launch operations, and the numbers in §4
are illustrative targets, not committed budget.

Written in response to [MARKET.md](MARKET.md) §5.2's "No supply acquisition
plan" gap (Critical: *"How the first hundred Jaipur technicians get
recruited, police-verified, trained, equipped, uniformed and retained.
In-home services live or die on this, and it has a longer lead time than
any software task on the list."*) and directly implements MARKET.md §4 move
#2: *"Buy supply, not demand... Offer the first 300–500 Jaipur technicians a
materially lower take rate plus a guaranteed monthly job floor... paying to
own the city's best tradespeople."*

This document does not own competitive analysis or the margin thesis behind
*why* supply is the lever — [MARKET.md](MARKET.md) keeps those. It owns the
recruitment *mechanics*: sourcing, vetting pipeline, the commercial offer to
a technician, and retention.

## 1. WHY SUPPLY, NOT DEMAND, FIRST

Per MARKET.md §3, quality of supply *is* the product in home services — a
customer's entire experience is one technician, once, in their home. Per
§2.1, Urban Company's ~28% take rate is high enough to have triggered
organised partner protests; per §2.3, unbranded local operators (Jaipur Home
Service and peers) have real, experienced tradespeople with zero platform
lock-in. Both are the same opportunity: **the best technicians in Jaipur are
already working, just not on a platform that treats them well.** Winning
them costs money and process, not a new invention — which is exactly why
this is a Critical gap and not an engineering task.

## 2. SOURCING CHANNELS

Ranked by expected cost-per-qualified-technician, cheapest first:

1. **Local operator technicians (MARKET.md §2.3).** Jaipur Home Service and
   similar generalist operators have rosters of experienced, un-locked-in
   tradespeople. Direct outreach — not a hostile poach, a better offer (§4)
   — is the fastest path to experienced supply that already knows how to do
   the job; it just needs a platform.
2. **Urban Company technicians in Jaipur (MARKET.md §2.1).** The highest-skill
   pool in the city, already trained to platform-service standards (app use,
   customer interaction, punctuality). The ~28% take rate is the lever —
   Nestly's lower rate plus a job-floor guarantee (§4) is a concrete,
   quantifiable pitch, not a vague "better deal."
3. **Trade schools and ITIs (Industrial Training Institutes).** Jaipur has
   several ITIs producing electricians, plumbers and appliance technicians
   annually. A standing relationship (campus recruitment, a referral
   commission to placement cells) is a slower but renewable channel,
   important once the first-mover pool above is exhausted.
4. **Referral from onboarded technicians.** Once the first cohort is live,
   technician-refers-technician is typically the cheapest channel of all —
   the same principle [REFERRAL.md](REFERRAL.md) already models for
   customer acquisition, applied to supply. A technician-referral bonus
   (paid on the referred technician's Nth completed job, not on signup, to
   discourage low-quality referrals) is a natural Phase 2 addition to that
   module, not scoped here.

## 3. VETTING PIPELINE (mechanics already exist)

Per the "reuse, don't duplicate" principle — the verification pipeline this
plan needs is already built, per the admin-web QA walkthrough (`/providers`
detail page: photo/KYC approve-reject controls, background check, dispatch
capacity editor). This document's job is the *policy* riding on top of that
pipeline, not new schema:

| Existing mechanism | What it does | Policy this plan adds |
|---|---|---|
| `Provider` | Core profile, service areas, dispatch capacity | Minimum service-area and category coverage before activation (not currently policy-gated — see OPEN DECISIONS) |
| `ProviderKycDocument` | Identity/document upload and admin approve-reject | **Required before first job**, not before payout — a technician should not wait to earn while paperwork clears, but must clear it before being dispatched |
| `ProviderBackgroundCheck` | Background/police verification record | **Required before first job**, same reasoning. Police verification specifically (a Jaipur-specific document type) needs to be confirmed as a supported check type — see OPEN DECISIONS |
| `BookingCompletionProof` | Photo/checklist proof required to close a job | Doubles as **on-the-job quality evidence** during a technician's probation window (§4) — a new hire's first N jobs get a lightweight admin spot-check of completion proof, not a new mechanism |

Proposed pipeline order: **application → KYC document upload → background
check → skills verification (a short practical/interview, not currently
modeled in code — operational, not a system step) → service-area and
category assignment → first-job probation window → full activation.**

## 4. THE COMMERCIAL OFFER

Per MARKET.md §4 move #2, the offer is two parts, both of which need a real
number from the unit-economics model (§5) before this becomes a commitment
technicians can be recruited against:

1. **A materially lower take rate than Urban Company's ~28%.** The exact
   number is an unit-economics decision (see OPEN DECISIONS #1) — it must
   be low enough to be a credible pitch against an incumbent's public
   number, while still clearing the margin the AMC/contract thesis (MARKET.md
   §3) depends on.
2. **A guaranteed monthly job floor**, funded by the contract book MARKET.md
   §4 move #1 (society contracts) generates — a fixed daily technician route
   through a signed building is what makes a job-floor guarantee solvent
   rather than a subsidy Nestly pays out of pocket indefinitely. This is
   why MARKET.md §6 sequences "sign societies" and "recruit supply" as
   parallel, not sequential, tracks: the job floor's funding source and the
   technicians it needs to cover both have to exist near-simultaneously.

Equipment and uniforming (a branded kit — the trust signal a customer sees
at the door) is a one-time per-technician cost, budgeted in the
unit-economics model as part of technician onboarding cost, not modeled
here.

## 5. UNIT ECONOMICS DEPENDENCY

Same dependency [PRICING.md](PRICING.md) states for the customer-facing
price book: the take rate and job-floor guarantee in §4 are policy shapes,
not numbers, until the costed unit-economics model (MARKET.md §6 step 2)
checks them against what a job actually costs to deliver. This document
does not set the take rate — it states what the offer needs to contain.

## 6. RETENTION

Recruitment without retention just repeats the cost. Three mechanisms, in
order of how directly they're already supported:

1. **The job-floor guarantee itself (§4)** is the primary retention lever —
   a technician who trusts next month's income doesn't shop a better rate
   elsewhere.
2. **Quality-linked standing** — `Provider`'s existing rating/performance
   tracking (visible in the admin-web `/providers/[id]` performance stats
   the QA walkthrough confirmed) is the natural input to a tiered
   take-rate-discount-for-quality structure (a top-rated technician earns a
   *better* rate over time), a policy layer on existing data, not new
   tracking.
3. **Referral bonus (§2.4)** turns a retained technician into a recruiting
   asset rather than a sunk cost.

## OPEN DECISIONS (need business/ops sign-off)

1. **The actual take-rate number.** This document states "materially lower
   than ~28%" per MARKET.md; the specific figure is a unit-economics
   output, not decided here.
2. **Whether police verification is a hard gate or a fast-tracked parallel
   step.** A strict "no dispatch until police verification clears" policy
   is safer but slower to build initial supply; a "provisional activation,
   full activation pending clearance" policy is faster but carries real
   liability exposure — see [INSURANCE.md](INSURANCE.md) for how that
   exposure is covered in the interim, if at all.
3. **Job-floor guarantee size and funding trigger.** How many technicians
   the guarantee extends to, and whether it activates before or only after
   the first society contracts are signed (MARKET.md §4 move #1), is an
   operating-capital decision.
4. **Uniform/equipment cost ownership.** Whether Nestly fronts the cost
   (higher CAC per technician, no barrier to signing) or the technician
   buys in (lower CAC, but a friction point against a free-to-join
   incumbent) — needs an operations call.

## NEXT STEPS

Not a `tasks.csv` phase — this is an operational plan awaiting sign-off, per
this document's own STATUS. Once approved:

1. Business/ops sign-off on §4's offer shape and §OPEN DECISIONS.
2. Unit-economics model sets the actual take rate and job-floor budget.
3. Any net-new engineering this surfaces (e.g., a technician-referral bonus
   on `REFERRAL.md`'s existing mechanism, or a policy gate enforcing
   KYC/background-check completion before first dispatch if one doesn't
   already exist — verify against current `ProviderStatus` handling before
   assuming a gap) gets scoped as its own phase, not built ad hoc from this
   document.

# MARKET.md

Market context, competitive intelligence, revenue-model thesis and launch
strategy for the **Jaipur** launch market.

## STATUS

Analysis dated **2026-08-17**, pre-launch. Jaipur is the confirmed first
market.

This document owns *business* strategy — market, competitors, revenue models,
pricing posture and the commercial gap register. It does not own feature
specifications: subscription, recurring bookings, in-app chat and completion
verification are specified in
[PRODUCT-ENHANCEMENTS.md](PRODUCT-ENHANCEMENTS.md); repository state is
described in [ORIENTATION.md](ORIENTATION.md); product vision and the module
inventory are in [PROJECT.md](PROJECT.md).

**Confidence note.** Nestly's actual cost structure, funding position, team
size and target launch date were not available when this was written. Every
rupee figure below is derived from published market rates and industry
benchmarks, *not* from a Nestly costed model. Treat them as illustrative
structures that show relative order of magnitude, and replace them with real
numbers once the unit-economics model exists (see
[§6 Sequencing](#6-sequencing)). Competitor detail for the smaller Jaipur
operators comes from their own public marketing, which tends to be generous
about coverage and response times.

---

## 1. MARKET CONTEXT

Two facts shape everything that follows.

**This is not a share war.** Roughly **98%** of India's ~$60B home-services
market is still offline — a neighbourhood electrician's number saved in
someone's contacts. Urban Company and every app below it compete over a
sliver. Nestly does not need to take customers from Urban Company; it needs to
convert offline habit into booked, recorded, repeatable transactions.

**Jaipur is unusually well-timed.** The city posted **98.2%** property price
growth in 2025 — the highest of any Indian tier-2 city — against **16.7%** GDP
growth versus a 7.7% national average. A city adding apartment stock at record
pace is a city creating brand-new service relationships: households with no
incumbent plumber, no trusted maid, no AC technician. Those households are
cheap to acquire and, critically, they form habits.

---

## 2. COMPETITIVE LANDSCAPE — JAIPUR

Seven players, assessed by what they can and cannot do. This is the
authoritative competitor list for the launch market.

### 2.1 Urban Company — national, full-stack, listed · HIGH THREAT

**Has.** Live in Jaipur across salon, cleaning, painting, AC and appliance
repair, electrician, plumber and carpenter. Trained and vetted professionals,
standardised pricing, warranty, insurance, a genuinely good app, post-IPO
capital. Roughly 60% contribution margin on Indian consumer services; over 30%
of repeat users on a paid membership.

**Lacks.** Partner commission runs around **28%** — high enough that their
best technicians have a standing incentive to take repeat customers
off-platform, and high enough to have triggered organised partner protests.
Customers report surge pricing, add-on charges, no-shows and slow refunds;
independent review sites skew heavily negative despite the in-app rating.
Support is national and remote — no Jaipur ops presence to escalate to. And
they have **no B2B or contract offering at all**.

**Take.** Not their customers — their **technicians**. A materially lower take
rate plus guaranteed recurring volume is the cheapest supply-acquisition lever
in this market, and it attacks the one structural weakness capital cannot fix.

### 2.2 Local Pintu — Jaipur local operator · MEDIUM THREAT

**Has.** The strongest local trust signal in the city: a **three-month free
service warranty** on appliance, repair and cleaning work. Fast-growing,
locally known, priced for Jaipur rather than for Gurgaon.

**Lacks.** The warranty is a promise with no system behind it — no
photographic job record, no digital service history, no auditable ledger of
what was done. Disputed claims come down to argument. No recurring plans, no
live tracking, no real payment rails.

**Take.** Match the warranty, then out-prove it. Completion photos, a
checklist and a permanent per-appliance service record turn an unverifiable
promise into a receipt — and make the warranty cheaper to honour, because you
can see what actually happened.

### 2.3 Jaipur Home Service — local generalist · MEDIUM THREAT

**Has.** Broad coverage — electrician, plumber, carpenter, mechanic, painter,
AC repair — at local prices, with years-deep relationships with Jaipur
tradespeople.

**Lacks.** A website and a phone number. No slot engine, no confirmed
appointment times, no payment infrastructure, no vetting record, no SLA, no
accountability once the technician leaves.

**Take.** Treat them as a **supply channel, not a rival**. Their technician
roster is exactly the pool Nestly needs to recruit, and they have no lock-in
to offer against a better rate plus steady work.

### 2.4 Raypid — electrical and plumbing · LOW THREAT

**Has.** A 24/7 availability promise and phone-first booking — which quietly
serves a large segment app-only players miss entirely: older customers, and
anyone who wants to *speak* to someone before letting a stranger into their
home.

**Lacks.** Phone-driven dispatch does not scale past a handful of jobs a day.
No tracking, no record, no repeat mechanism, narrow category range.

**Take.** The lesson, not the market. **Do not launch app-only in Jaipur.**
WhatsApp and phone bookings landing in the same pipeline as app bookings
expand the addressable market at near-zero cost.

### 2.5 ServiceOnWheel — plumbing specialist · LOW THREAT

**Has.** A sharp, memorable promise: plumbing repairs within 45 minutes. Speed
is a real differentiator in an emergency category.

**Lacks.** Single category, and the SLA is unverifiable — no tracking, no
timestamp, no consequence when missed. A promise with no penalty is marketing,
not a service level.

**Take.** A **measured** SLA. Nestly's live order tracking
([TRACKING.md](TRACKING.md)) supports timestamped arrival; pairing it with
automatic credit when the window is missed converts a slogan into something a
competitor cannot claim without the same infrastructure.

### 2.6 Justdial · Sulekha · IndiaMART — directories, lead resale · LOW THREAT

**Has.** Enormous top-of-funnel. For many Jaipur customers this is still the
first search, and they own the SEO for "plumber in Jaipur".

**Lacks.** They sell the same lead to five vendors. The customer gets five
calls, five prices and no accountability — a genuinely unpleasant experience
that persists only because nothing better is locally available.

**Take.** Position explicitly against it: **one price, one named person, one
confirmed time**. Note also that they compete for the *same technicians*, by
selling them leads. Nestly offers those technicians work instead of bills.

### 2.7 Snabbit · Pronto — instant home help · NOT YET IN JAIPUR

**Are.** Well-funded instant-help players — helpers dispatched in 10–15
minutes for cleaning, dishwashing, laundry and kitchen prep. Snabbit raised
$56M across 2025; Pronto raised $25M to expand. With Urban Company's InstaHelp
they remain under 1% of the market, but are growing fast.

**Why they matter.** Metro-only today. Tier-2 expansion is the obvious next
step and Jaipur is near the front of that queue. Assume **12–24 months**
before instant-help lands here with venture pricing behind it.

**Response.** Do not enter the speed race — it is a funding race and Nestly
loses it. Spend the window building what they structurally cannot copy
quickly: **signed recurring contracts**. A society under a twelve-month
agreement is not available to a discount blitz.

---

## 3. WHERE THE MARGIN IS

Annual gross profit per acquired relationship. Illustrative — see the
confidence note in STATUS.

| Revenue type | Annual gross profit | Basis |
|---|---:|---|
| One-off consumer job | ≈ ₹120 | ₹599 AC service at ~20% take, less gateway fees, support cost and redo provision |
| Repeat app customer (4 jobs/yr) | ≈ ₹480 | Breaks even on acquisition somewhere in year one |
| Appliance AMC (annual contract) | ≈ ₹1,400 | ₹2,000–8,000 contract paid upfront, delivered across the year |
| Household recurring plan (weekly) | ≈ ₹5,200 | Weekly cleaning at ₹500 — ₹26,000 annual booking value from one acquisition |
| B2B site contract (per site) | ≈ ₹1.9L | Office/clinic/showroom/PG at ₹35,000/month at typical facility-services margin |
| Society / RWA contract | ≈ ₹2.9L+ | 200-flat society, common-area, lift, plumbing and pest |

The spread between the top and bottom rows is roughly **2,000×**.

Three observations matter more than the individual figures:

- **The one-off job loses money.** Against a realistic ₹300–600 acquisition
  cost, ₹120 of contribution does not recover CAC, and one-off customers
  return roughly once a year. Every competitor above — including Urban
  Company — makes most of its money from this transaction. It is the worst
  one.
- **AMC has the best cash-flow profile in the catalogue**: cash arrives before
  cost, and renewal acquisition cost is effectively zero. The industry's own
  weakness here is instructive — providers lose **15–25% of AMC revenue** to
  missed renewals and manual tracking. That is a software problem, and
  software is what Nestly has.
- **A society contract is not primarily a contract.** It puts a Nestly
  technician inside the gate every day, in front of ~200 households that can
  then be sold in-home services at essentially zero acquisition cost.

**Strategic conclusion.** The consumer app is the shop window, not the
business. Build the consumer experience well enough to be credible and to
generate trust — then make the money on contracts. Urban Company cannot follow
there without building a different company. The local players cannot follow
there without building software.

---

## 4. SIX MOVES FOR JAIPUR

Ranked by leverage per rupee of effort.

**1. Sell the society before you sell the household.** Go door-to-door on
*buildings*, not doors. Sign apartment societies for common-area housekeeping,
lift and pump maintenance, tank cleaning and pest control. Each signature is a
year of revenue, a fixed daily technician route, and a standing presence in
front of a few hundred households. Jaipur is adding apartment stock faster
than any tier-2 city in India, and new societies are actively shopping for
their first maintenance vendor — that window closes once they sign someone
else.

**2. Buy supply, not demand.** Urban Company takes ~28% and its partners have
publicly protested the terms. Offer the first 300–500 Jaipur technicians a
materially lower take rate *plus* a guaranteed monthly job floor funded by the
contract book from move 1. This is not discounting to customers — it is paying
to own the city's best tradespeople. In home services, quality of supply *is*
the product, and no cheaper supply-acquisition window will exist again once a
funded competitor arrives.

**3. Make transparent pricing the brand, not a feature.** The most consistent
complaint against Urban Company is price behaviour — peak-season surge,
add-ons discovered at the door, quotes that move. Commit publicly to a single
all-in price shown before booking, no surge, no at-door upsell, and pay the
customer if the technician quotes differently. Cost: a pricing policy and some
UI. Value: it names the largest competitor's most-resented behaviour, in a
market more price-sensitive than the metros where that behaviour was designed.

**4. Take bookings on WhatsApp from day one.** Urban Company is app-only; the
local players are phone-only. Neither serves the large middle — people who
will happily message but will not install and learn an app to get a tap fixed.
Route WhatsApp and phone bookings into the same pipeline so they carry the
same record, tracking and warranty. This is also the only realistic path into
peri-urban Jaipur (Sanganer, Chomu, Bagru, Jagatpura's fringe), where app
penetration is thin and no competitor operates.

**5. Sell to Jaipur's hospitality layer.** Jaipur is a tourism city with an
enormous long tail of guest houses, homestays, boutique hotels and serviced
apartments — every one needing turnover cleaning, laundry, AC servicing and
plumbing on a predictable schedule, and preferring a contract to a call.
Structural bonus: hospitality demand peaks in the morning and midweek,
precisely when consumer household demand is lowest. The same technician roster
covers both, and utilisation is where service-business margin comes from.

**6. Turn the warranty into a receipt.** Local Pintu's three-month warranty is
the strongest trust claim in Jaipur and it is entirely unverifiable. The
planned completion-verification work (photo proof and a checklist required
before a job can close — see
[PRODUCT-ENHANCEMENTS.md](PRODUCT-ENHANCEMENTS.md)) turns the same promise
into an auditable per-appliance, per-visit record. Second-order benefit: a
device-level service history is the natural hook for an AMC renewal
conversation, the highest-margin repeat sale in the catalogue.

---

## 5. GAP REGISTER

Nestly's engineering position is strong for a pre-launch product — identity,
catalog, booking, payments, wallet, reviews, support, notifications, admin and
live order tracking are built or substantially built on a clean modular
monolith. The gaps below are not about code quality. They are the distance
between what exists and what §3 and §4 require.

### 5.1 Product gaps

> **Corrected 2026-08-17.** This section originally listed subscription,
> recurring bookings, completion verification, referral, in-app chat and
> automatic provider assignment as unbuilt. That was wrong: it was written
> against four module specifications whose `STATUS` headers still read *"Not
> implemented"* for modules delivered phases earlier. All six are implemented.
> The three Critical business-model gaps below were re-verified against `main`
> and confirmed absent. See
> [LAUNCH-READINESS-AUDIT.md](LAUNCH-READINESS-AUDIT.md) for the evidence and
> the corrections applied across the documentation suite.

| Gap | Severity | Why it blocks the strategy |
|---|---|---|
| No B2B account model | Critical | The data model runs *person → booking*. No organisation, contract, site, purchase order, invoice, GST handling, net-30 billing or multi-user account hierarchy — confirmed absent in `shared/Domain`. **Every high-margin move above is unbuildable without it** — and with the consumer product further along than believed, this is now the single binding engineering constraint on the revenue strategy. |
| No AMC / entitlement model | Critical → **in progress** | An AMC is prepaid entitlement drawn down over twelve months, with scheduled preventive visits and a renewal pipeline. Domain model, migration and specification now exist ([AMC.md](AMC.md), Phase 20, `tasks.csv` #323–#330) — application services, API surface and both frontends remain open. See [AMC.md](AMC.md) STATUS and ORIENTATION.md for current build state, not this row. |
| No WhatsApp booking channel | High | WhatsApp exists only as a `CustomerCommunicationPreference` notification channel, not a booking intake path. This closes off the peri-urban and non-app segments entirely. |
| Release readiness unverified | High | The backlog is closed, but `tasks.csv` task 318 remains open: [QA-REPORT-2026-08-07.md](QA-REPORT-2026-08-07.md) returns **NO-GO for release on absence of evidence** — 587 inventoried UI features are runtime-unverified and cross-service booking consistency is unmeasured. No launch date is defensible until this is executed. |
| Test coverage uneven | Medium | 1,363 declared test methods (1,767 executed cases at the last green run), but the bulk live in one misnamed `Catalog.Tests` project while `CustomerManagement.Tests` has 12. The last full build-and-test run predates Phase 18 by 85 commits. |
| Subscription, recurring bookings, completion verification, referral, chat, auto-assignment | ~~Critical/High/Medium~~ **Built** | Previously listed here as unbuilt. All implemented — entities, migrations, services, endpoints, frontend pages and tests. Verified 2026-08-17. |

### 5.2 Business and operational gaps

| Gap | Severity | What is missing |
|---|---|---|
| No costed unit economics | Critical → **model drafted** | No per-job P&L: price, technician payout, consumables, travel, gateway fees, support cost, redo and refund rate, CAC amortisation. Every figure in §3 is a market-derived estimate standing in for a model Nestly needs to own. A costed spreadsheet model now exists — [assets/nestly-unit-economics.xlsx](assets/nestly-unit-economics.xlsx) (Assumptions, Revenue Streams, Summary) — pending final business-input review and a first open-in-Excel confirmation (see its Read Me tab) before its figures supersede the estimates in §3. |
| No pricing strategy | Critical → **recommendation drafted** | No price book, no city rate card, no surge policy, no contract pricing tiers. Pricing is the primary stated differentiator against Urban Company. A pricing posture recommendation now exists — [PRICING.md](PRICING.md) — pending business/legal sign-off; nothing in it is implemented policy yet. |
| No supply acquisition plan | Critical → **plan drafted** | How the first hundred Jaipur technicians get recruited, police-verified, trained, equipped, uniformed and retained. A recruitment plan recommendation now exists — [SUPPLY.md](SUPPLY.md) — pending ops sign-off; recruitment has not started. |
| No liability / insurance model | High → **recommendation drafted** | Urban Company covers damage. One broken marble floor without cover is an existential trust event for a new brand, and it is the first question a society secretary asks. A coverage-shape recommendation now exists — [INSURANCE.md](INSURANCE.md) — pending legal/insurance sign-off; no actual coverage exists yet. |
| GST and contracting posture undecided | High → **recommendation drafted** | Whether Nestly transacts as agent (commission) or principal (resells the service) changes GST treatment, invoice format and contract structure. B2B customers cannot be invoiced until this is settled. The decision is framed — [GST.md](GST.md) — pending CA/tax counsel determination; still undecided. |
| No local ops footprint | Medium | A Jaipur ops lead and a small stores point for consumables and spares are what make same-day SLAs physically possible — and a local human to escalate to is precisely what Urban Company cannot offer here. |
| Launch readiness is not verifiable | High → **narrowed** | Audited 2026-08-17: the documentation defects behind this risk are fixed (see [LAUNCH-READINESS-AUDIT.md](LAUNCH-READINESS-AUDIT.md)), and the code surface is confirmed present. What remains is not a documentation problem but an outstanding QA execution — task 318, carrying a **NO-GO** verdict on absence of runtime evidence. Still a business risk; no longer an unknown one. |

---

## 6. SEQUENCING

Ordered by dependency, not by appetite.

1. ~~**Audit what actually works.**~~ **Done 2026-08-17** — see
   [LAUNCH-READINESS-AUDIT.md](LAUNCH-READINESS-AUDIT.md). The documentation
   defects are fixed and the code surface is confirmed. What replaces this
   step is **task 318**: execute QA phases 3 and 4, which carry the standing
   **NO-GO** verdict. That is now the gate on any launch date.
2. **Build the costed unit-economics model.** A spreadsheet, not code. It
   determines the take rate, the technician offer, and whether the contract
   thesis survives contact with real numbers.
3. **Design the B2B account model** — organisation, site, contract,
   entitlement, invoice. The largest architectural addition, and it gates the
   highest-margin revenue. Start the design while the audit runs.
4. ~~**Ship subscription and recurring bookings.**~~ **Void — already
   shipped.** Both are implemented end-to-end, including the Hangfire
   occurrence-generation job. Backlog rows `#296`–`#300` duplicate delivered
   work and are now closed on `main`. The differentiator exists; the open
   questions are whether it works at runtime (task 318) and whether anyone can
   buy it. See [LAUNCH-READINESS-AUDIT.md](LAUNCH-READINESS-AUDIT.md) §5.
5. **Recruit supply in parallel, starting now.** Technician recruitment and
   verification has the longest lead time of anything here and does not depend
   on software being finished.
6. **Sign three societies before launch.** Not as revenue — as proof. Three
   signed contracts validate the entire thesis before anything is spent on
   consumer acquisition, and if they are hard to sign, that is a cheap lesson.

---

## 7. OPEN QUESTIONS

Two things worth verifying directly before acting:

- **Urban Company's real category and pincode coverage within Jaipur.** Their
  weakest flank is likely the periphery, but that should be checked, not
  assumed.
- **Whether a Jaipur facility-management incumbent already holds the society
  contracts** described in move 1.

---

## SOURCES

Public research, August 2026.

- RedSeer — *Instant Home Services and the Next Habit Loop*
- IMARC — *India Online On-Demand Home Services Market*
- Urban Company FY25 financial analysis and revenue streams
- ICICI Direct — Urban Company IPO review
- Urban Company — partner protests and company history
- Urban Company customer reviews (PissedConsumer)
- Urban Company Jaipur — categories and pricing
- Local Pintu (Jaipur), Jaipur Home Service, Raypid, ServiceOnWheel — public
  marketing sites
- Business Today — Snabbit, Urban Company and Pronto
- Business Standard — Pronto raises $25M
- TechCrunch — Lightspeed backs Snabbit
- Jaipur property price growth 2025; Jaipur real estate market trends
- AMC contracts — process and revenue leakage
- Local Jaipur AC service rates (Keyvendors)

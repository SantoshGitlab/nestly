# PRICING.md

Pricing strategy recommendation for the Jaipur launch: what price is shown,
when it changes, and what happens when it doesn't match reality.

## STATUS

**Recommendation, not policy.** This document proposes a pricing posture and
names the mechanics it needs. It does not set a price, and nothing in it is
implemented as a business rule beyond what §3 "WHAT ALREADY EXISTS" states
plainly. Pricing is a commercial and legal decision — margin floor, refund
liability, tax treatment — and needs sign-off from whoever owns P&L for the
Jaipur launch before any number here is treated as final.

Written in response to [MARKET.md](MARKET.md) §5.2's "No pricing strategy"
gap (Critical: *"No price book, no city rate card, no surge policy, no
contract pricing tiers. Pricing is the primary stated differentiator against
Urban Company and is currently undesigned."*) and directly implements the
policy shape MARKET.md §4 move #3 names: *"Make transparent pricing the
brand, not a feature... a single all-in price shown before booking, no
surge, no at-door upsell, and pay the customer if the technician quotes
differently."*

This document does not own revenue-model thesis, competitive positioning, or
margin-per-revenue-type figures — [MARKET.md](MARKET.md) keeps those. It
owns the pricing *mechanics and policy*: what the price book looks like,
what "all-in" means operationally, and what the quote-guarantee commits to.

## 1. THE POSITION

Urban Company's most consistent customer complaint, per MARKET.md §2.1, is
price behaviour — not price *level*. Surge pricing at peak times, add-ons
discovered at the door, a quote that moves between booking and completion.
Jaipur is more price-sensitive than the metro markets that behaviour was
designed for, which makes it a sharper wedge here than it would be in Delhi
or Bangalore.

**The recommended position: one price, shown before booking, honoured at the
door.** Not the cheapest price — the most *trustworthy* one. A customer who
has been burned by a moving quote once will pay a premium for a platform
that credibly commits not to do it again.

Three commitments make up the position:

1. **All-in price shown before booking.** What the customer sees at
   checkout is what they pay — service charge, applicable tax, and any
   platform fee, summed into one number. No fee introduced after
   confirmation.
2. **No surge pricing.** Price does not change by time of day, day of week,
   or demand load. `ServiceCityPrice` already supports geography- and
   time-bound price *changes* (city overrides, effective-date ranges for
   planned revisions) — that mechanism is for deliberate admin repricing
   (a new rate card, a promotional window), never for automated
   demand-responsive pricing. No surge/dynamic-pricing engine exists in the
   codebase today, and this document's recommendation is that none gets
   built. If that recommendation is ever reversed, it must be reversed
   explicitly here, not introduced quietly as a "smart pricing" feature.
3. **Quote guarantee.** If the technician's on-site assessment requires a
   price different from what was booked (a job turns out more complex than
   the category's flat price assumed), the customer is never charged the
   difference without opting in, and if a provider *attempts* to collect
   more than the booked price without going through the change-order flow
   below, Nestly refunds the difference to the customer — a real financial
   commitment against provider behaviour Nestly doesn't fully control, not
   a slogan. See §4.

## 2. WHY THIS IS DEFENSIBLE, NOT JUST NICE

Per MARKET.md §3, the one-off consumer job is the lowest-margin line in the
catalogue (≈₹120 annual gross profit, and negative against realistic CAC).
A pricing policy that costs a little more to operate (the quote-guarantee
payout, foregone surge revenue) is not undermining the margin thesis,
because the one-off job was never where the margin was designed to come
from — AMC, recurring plans, and contracts are. Transparent pricing is the
*trust mechanism* that makes a stranger sign a 12-month AMC or let a
technician into a flat weekly: nobody commits to a recurring relationship
with a company whose per-visit price already felt unpredictable.

This is also why the quote-guarantee payout is bounded, not open-ended: it
protects trust on the acquisition transaction, funded by margin the
retention transactions are expected to generate. Section §5's unit-economics
model is where that trade is actually checked against real numbers, not
asserted here.

## 3. WHAT ALREADY EXISTS (mechanics this policy reuses)

Per the "reuse, don't duplicate" principle every module in this suite
follows — this policy is a constraint on pricing *behaviour*, not a new
pricing engine:

| Existing mechanism | What it does | How this policy uses it |
|---|---|---|
| `Service.Price` | The base flat price for a service category | The default "all-in" number shown at checkout |
| `ServiceCityPrice` | City-specific override, time-bound via `EffectiveStartDate`/`EffectiveEndDate` | Deliberate city rate-card differences (Jaipur peripheral pincodes vs. central) — **not** a surge mechanism; a row here is an admin decision with a start date, not an automated response to demand |
| `PromotionalPrice` | Time-bound discount, optionally city-scoped, admin-activated | Launch offers and seasonal campaigns — visible before booking, same "shown, not sprung" rule as the base price |
| `Coupon` | Code-entered discount at checkout (distinct from `PromotionalPrice` per its own doc comment) | Referral and marketing discounts; `CouponSettings.maxDiscountPercentagePerCoupon` and `allowCouponStacking` (admin-configurable, `docs` "System settings" SRS 12.19) already cap how far a coupon can move the shown price |
| `TaxSettings` (`defaultTaxPercentage`, `taxInclusivePricing`) | Admin-configured tax rate and whether displayed prices already include it | The "all-in" number is generated by this flag being `true` — the customer never sees a sub-total that grows at payment |
| `AmcPlan.Price` | Prepaid entitlement price ([AMC.md](AMC.md)) | Same all-in-shown-upfront rule; an AMC is the clearest case where "one price, no surprises" is the entire sales pitch |

**Nothing above needs new database schema for the position in §1.** What is
missing is (a) a policy decision to *never* populate a surge mechanism that
doesn't exist yet, (b) the quote-guarantee workflow (§4), which is new, and
(c) an operational commitment that at-door add-ons follow the change-order
flow rather than a verbal request for more cash.

## 4. THE QUOTE-GUARANTEE WORKFLOW (proposed, not built)

This is the one genuinely new piece of mechanics the position in §1 needs.
Proposed shape, for engineering scoping once the business signs off on the
commitment itself:

1. Customer books a service at its shown all-in price.
2. On-site, if the provider determines the job needs more (materials, time,
   a different category entirely — e.g. a "tap repair" that turns out to be
   a full pipe replacement), the provider submits a **change-order request**
   through the provider app: new price, reason, optionally a photo.
3. The customer approves or declines in the customer app, in real time,
   before any extra work starts. Decline means the original scope completes
   at the original price, or the customer cancels that portion penalty-free
   — never "pay more or we walk."
4. If a provider collects money outside this flow (cash, UPI, anything not
   through the change-order), and the customer reports it, Nestly refunds
   the excess to the customer from its own margin and the incident feeds
   the provider's quality record — the same standing consequence pattern
   `PROVIDER.md`'s quality/rating mechanics already establish for other
   provider misconduct.

This reuses `Booking`'s existing item/price-snapshot shape (a change order
is a priced addition to a booking's item list, the same snapshot-at-transaction
discipline every other pricing feature in this codebase follows) rather than
inventing a parallel pricing object — but the workflow itself, the two apps'
UI for it, and the refund-liability accounting are unbuilt. Not claimed done
here.

## 5. UNIT ECONOMICS DEPENDENCY

MARKET.md §5.2 pairs this gap with "No costed unit economics" for a reason:
a pricing *policy* can be designed without real numbers, but a pricing
*book* — what a category actually costs, what margin a price defends —
cannot. The costed unit-economics model (spreadsheet, not code, per
MARKET.md §6 step 2) is where the take rate, technician payout floor, and
quote-guarantee liability budget actually get set. This document states the
policy shape; that model is where the numbers that make the policy solvent
get checked.

## 6. OPEN DECISIONS (need business/legal sign-off)

1. **Quote-guarantee payout ceiling.** An uncapped commitment to refund any
   at-door overcharge is a real liability with no upper bound until a policy
   caps it (e.g. per-incident cap, or refund-plus-credit rather than
   unlimited cash). Needs a number from whoever owns the P&L.
2. **City rate-card differences vs. the "one price" promise.** `ServiceCityPrice`
   supports a materially different Jaipur-periphery price than central
   Jaipur. That is defensible (delivery cost genuinely differs), but the
   customer-facing "no surge" claim must be worded to distinguish "your
   city/area has its own listed price" from "your price changed based on
   when you're booking" — legal/marketing sign-off on the exact language
   customers see.
2. **Promotional-price cadence.** How often `PromotionalPrice` campaigns run
   and how deep they go is a marketing decision this document does not make
   — only that any promotion is shown before booking, never sprung.
3. **Whether the quote-guarantee applies to AMC-redeemed visits.** An AMC
   visit is already zero-priced at redemption ([AMC.md](AMC.md)); the
   guarantee's relevant question there is whether a provider can decline to
   perform a covered visit and demand payment instead. Needs the same
   policy answer as a paid booking, extended to a zero-price context.
4. **GST treatment of the all-in shown price.** Whether the platform
   transacts as agent or principal (see the open GST posture question this
   document does not own — GST.md) changes what "all-in" legally means on
   the invoice, not just at checkout. This document assumes tax-inclusive
   display is a UI decision; GST.md governs whether it's also the correct
   legal invoice structure.

## NEXT STEPS

Not a `tasks.csv` phase yet — this is a recommendation awaiting sign-off,
per this document's own STATUS. Once approved:

1. Business sign-off on §1's three commitments and §6's open decisions.
2. Unit-economics model (§5) sets the actual price book and the
   quote-guarantee payout ceiling.
3. Engineering scoping for the change-order workflow (§4) as its own
   phase, sized like any other module in this suite (domain model,
   application services, API surface, both frontends).

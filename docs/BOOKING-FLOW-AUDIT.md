# Booking flow audit — walking the funnel as a customer

> **Status: all ten findings fixed** (2026-08-05). Tracked as tasks 230-239 in
> `tasks.csv`, plus two follow-ups that need decisions rather than code: #240
> (expiring bookings abandoned at the payment step) and #241 (idempotency key
> on `POST /bookings`). Each finding below keeps its original description so
> the reproduction stays readable; the fix is recorded in its task row.
>
> Verification: 998 backend unit/integration tests, the 8-test Playwright e2e
> suite, and a browser pass that re-ran every original reproduction against the
> patched stack (8/8). One pre-existing latent problem surfaced while fixing
> finding 3 — the draft-persist effect wrote the page's initial state over the
> draft it was about to restore from, so the "restore on detour" feature had
> never actually worked; fixed alongside it.
>
> Repairing an existing environment also needs
> `database/scripts/reconcile-slot-capacity.sql` (finding 7).

Manual end-to-end pass over the customer booking journey (discovery → service →
summary → payment → confirmation), driven through a real Chromium browser
against the running stack (customer-web on :3000, consumer-api on :5257, real
Postgres/Redis). Every finding below was reproduced against the running app,
not inferred from reading code — code references are given for the cause.

The lens is deliberately behavioural: what a real person does when they are
impatient, distracted, second-guessing themselves, or trusting a default.

Date of pass: 2026-08-05 (22:00 IST). Seeded catalog: Bengaluru → Home
Cleaning → Deep Home Cleaning (₹1499).

---

## 1. The login wall throws away the booking the customer was making

**Behaviour:** Signed-out customer picks city → category → service → locality →
clicks **Book now**.

**What happens:** they land on `/login` with no memory of where they were.
After signing in, the app puts them on **`/profile`** — a form asking for their
date of birth. The booking they were one click from placing is gone. Pressing
Back from the login page does *not* return them to the booking either: it
returns to the service page, because the redirect used `router.replace`, so the
booking URL was never in history.

To get back they must redo: home → city → category → service → locality → Book.

**Why it matters:** this is the single highest-drop-off moment in any booking
funnel, and the app currently punishes the customer for reaching it. The
intent was captured (a service, a locality, a click) and then discarded.

**Cause:**
- `src/components/RequireAuth.tsx:31` — `router.replace("/login")`, no
  `returnTo`/`next` parameter.
- `src/app/login/page.tsx:197` and `:275` — both customer sign-in paths end in
  `router.push("/profile")` unconditionally.

**Fix shape:** carry the intended destination (`/login?next=<encoded path>`),
and have both sign-in handlers honour it, defaulting to `/profile` only when
absent.

---

## 2. An impatient customer creates several real bookings

**Behaviour:** customer on patchy mobile data taps **Proceed to book**. Nothing
visibly happens for a couple of seconds, so they tap again. And again.

**Reproduced:** with the connection throttled (1200 ms latency, 200 kbps —
ordinary mobile data), one customer's intent produced **three separate
bookings**, three `201 POST /api/v1/bookings`, three rows in `booking`, all
`PaymentPending`, all on the same slot. Only the last one is navigated to; the
other two are invisible to the customer at that moment and are never cleaned
up.

Observed button state after the first tap:

```
+1500ms: button still busy
+3000ms: button ENABLED — tapping again      <-- still on the summary page
+4500ms: button still busy
+6000ms: button ENABLED — tapping again
bookings created: 3
```

**Cause:** `src/app/booking/summary/page.tsx:306-315`. The success path
comments say the busy state is deliberately left on through the route
transition:

```js
router.push(`/booking/payment/${booking.id}?serviceSlug=${service.slug}`);
// Deliberately no reset here: the navigation is in flight and leaving
// the button busy stops a second submit during the route transition.
return;
} catch (err) { ... } finally {
  if (!inFlight.current) return;   // never true — inFlight is always true here
  inFlight.current = false;        // ...so the guard always resets anyway
  setIsSubmitting(false);
}
```

`inFlight.current` is set to `true` at the top of the handler and is only ever
set back to `false` inside this `finally`. So on the success path
`!inFlight.current` is always `false`, the early return never fires, and the
button is re-enabled while the customer is still looking at the summary page.
The protection the comment describes is dead code.

**Note:** `src/app/booking/payment/[id]/page.tsx:144-148` has the identical
dead guard with the identical comment. A duplicate charge could not be
reproduced there (the success path navigates faster than the re-enable window),
but the guard is equally ineffective and the stakes are higher.

**Fix shape:** do not reset `inFlight`/`isSubmitting` on the success path at
all — e.g. `finally { if (!navigated) { inFlight.current = false; setIsSubmitting(false); } }`
with `navigated` set just before `router.push`. Server-side, an idempotency key
on `POST /bookings` would close the race properly.

---

## 3. Pressing Back before paying wipes everything, then creates a duplicate

**Behaviour:** the most ordinary hesitation there is — booking created, payment
page open, customer thinks *"wait, let me check I picked the right address"*
and presses Back.

**What happens:**
1. The summary page comes back **completely empty**: no address selected, date
   reset to today, no slot. Everything they entered is gone.
2. Today is then shown as "Fully booked" (see finding 8), so they must pick a
   date and slot again from scratch.
3. When they redo it and press **Proceed to book**, they get a **second,
   different booking**. The first is still sitting in `PaymentPending`.
4. `/bookings` → *Upcoming* now shows two identical "Deep Home Cleaning ·
   Thu, Aug 6 · Awaiting Payment · ₹1499.00" entries, with nothing to
   distinguish them and no hint that one is a stray.

A customer seeing two ₹1499 entries reasonably concludes they have been charged
twice.

**Cause:** `src/app/booking/summary/page.tsx:301` — `clearDraft(service.slug)`
runs as soon as the booking is created, so the sessionStorage draft the page
otherwise maintains (lines 41-92, added precisely so a detour doesn't cost the
customer their selections) is deleted before the customer can come back to it.
Nothing recognises that a `PaymentPending` booking for this exact
service/slot/address already exists.

**Fix shape:** keep the draft until payment succeeds, and on re-entering the
summary with a matching unpaid booking, offer to resume it
("You have an unpaid booking for this slot — continue paying / start over")
rather than silently creating another.

---

## 4. The service address is never checked against the city being booked

**Behaviour:** customer selects **Bengaluru** in the header, opens Deep Home
Cleaning, and books. The address radio is pre-selected for them — it is their
**default** address, "123 E2E Street, **E2E City** 560001". Nobody re-reads a
pre-selected default.

**Reproduced:** booking `27173623-…` was created and stored with:

```
address_city_snapshot     | E2E City          <- delivery address
address_pincode_snapshot  | 560001            <- not mapped to this service
slot_window_name_snapshot | Weekday Morning   <- a Bengaluru slot window
```

`service_pincode_mapping` maps Deep Home Cleaning to pincode 560034 only. The
booking is undeliverable, and the customer can pay for it.

**Cause:** the whole funnel validates against the **header locality** from
`localStorage`, never against the address the service is delivered to.
`BookingSummaryService.GetSummaryAsync`
(`backend/shared/Infrastructure/Services/BookingSummaryService.cs:60-87`) loads
the address only to check ownership:

```csharp
var address = await _addressRepository.GetByIdAsync(request.AddressId);
if (address is null || address.CustomerId != customerId) { ... }
...
var availability = await _slotAvailabilityService
    .GetAvailableSlotsAsync(request.ServiceId, request.LocalityId, request.SlotDate);
if (!availability.Value.IsServiceable)
    return Error.Business("Booking.NotServiceable",
        "This service is not available at the selected address.");
```

The error message says "at the selected address" while the check uses a
client-supplied `LocalityId` that has no relationship to that address. Both
seeded addresses have `locality_id = NULL`, so there is currently no link to
validate against at all.

**Fix shape:** derive serviceability from the selected address (require
`customer_address.locality_id`/pincode and validate against it), and reject a
request whose `AddressId` and `LocalityId`/`CityId` disagree. In the UI, warn
when the default address sits outside the selected city instead of silently
pre-selecting it.

---

## 5. Slots that have already finished are still bookable

**Behaviour:** at **22:17 IST** the availability API offered a slot window
running **17:00–19:00 today** — one that ended three hours earlier.

**Reproduced** (temporary window seeded for the test, since removed):

```
GET /api/v1/slots?...&date=2026-08-05
 -> { "slots": [ { "name": "TEMP Late Afternoon", "startTime": "17:00:00", "endTime": "19:00:00" } ] }

GET /api/v1/slots/revalidate?...      -> { "isValid": true, "reason": null }
POST /api/v1/bookings                 -> 201 Created
```

**Cause:** `backend/shared/Infrastructure/Services/SlotAvailabilityService.cs:64,82-85`

```csharp
DateTime now = _timeProvider.GetUtcNow().DateTime;
...
DateTime cutoffThreshold = now.AddMinutes(cutoffMinutes);
var slots = windows
    .Where(w => date.ToDateTime(TimeOnly.MinValue).Add(w.StartTime) >= cutoffThreshold)
```

The left side is a naive **local** slot datetime; the right side is **UTC now**.
In IST (UTC+05:30) the comparison is lenient by 5½ hours, so every window that
started up to 5½ hours ago still passes. The same skew silently widens or
narrows the configured `CutoffMinutes` for any non-UTC city.

**Fix shape:** compare in a single, explicit timezone — convert `now` to the
city's local time (or store slot times as UTC offsets) before applying the
cutoff. Worth a unit test pinned to a non-UTC `TimeProvider`.

---

## 6. A completely full slot is offered right up to the final click

**Behaviour:** customer picks a slot from the picker, reviews, and presses
**Proceed to book** — and only then is told the slot is full.

**Reproduced** with `max_bookings_per_slot = 1`:

```
POST /bookings (fills the single seat)   -> 201
GET  /slots?...                          -> slot still listed as available
GET  /slots/revalidate?...               -> { "isValid": true }
POST /bookings (the final click)         -> 409 Booking.SlotCapacityReached
```

**Cause:** `GetAvailableSlotsAsync`
(`SlotAvailabilityService.cs:43-90`) filters on serviceability, advance window,
blackout and cutoff, but **never reads `slot_booking_counter`**. Capacity is
enforced only by `ReserveSlotAsync`, called from `BookingService.cs:78` at
creation time.

This also contradicts the documented contract that `SlotPicker` is built on
(`src/components/SlotPicker.tsx:60-66`): *"the availability API only ever
returns the slots that ARE bookable … it never returns a disabled slot with a
reason"*. For capacity, it does exactly that — which is why the date strip
cannot grey out a full date.

**Fix shape:** join the counter in `GetAvailableSlotsAsync` and drop (or mark)
windows at capacity, and have `RevalidateSlotAsync` check it too so the
last-second re-check earns its name.

---

## 7. Slot capacity is consumed forever — cancellations never give it back

**Cause:** `SlotBookingCounter.BookedCount` is **only ever incremented**. The
single mutation in the codebase is
`SlotCapacityRepository.TryIncrementExistingAsync`
(`.../Repositories/SlotCapacityRepository.cs:68-70`); a repo-wide search for any
decrement / release path returns nothing. Neither `CancellationService` nor
`RescheduleService` references slot capacity at all.

Consequences, all compounding:
- A booking abandoned at the payment step (the norm, not the exception — and
  now multiplied by findings 2 and 3) holds its seat permanently.
- Cancelled and refunded bookings hold their seats permanently.
- `RescheduleService` (`.../Services/RescheduleService.cs:135-157`) validates the
  target slot but never reserves it, so rescheduling into a slot bypasses the
  cap entirely, while the vacated slot is never freed.

Live evidence on the seeded `E2E Anytime` window for 2026-08-06
(`max_bookings_per_slot = 50`):

```
slot_booking_counter.booked_count : 42
bookings actually on that slot    : 30   (12 Confirmed, 6 Completed, 12 Refunded)
```

The counter is 12 ahead of reality and 12 of the 30 are refunded, so ~24 of 50
seats are consumed by bookings that no longer exist or were cancelled. The slot
will start rejecting real customers at the final click (finding 6) while
actually being half empty.

**Fix shape:** release capacity on cancellation, refund and payment
expiry/abandonment; reserve on reschedule-in and release on reschedule-out.
A reconciliation job that rebuilds counters from live bookings would also fix
the drift already in the data.

---

## 8. "Fully booked" is shown when the truth is "too late for today"

**Behaviour:** opening the booking page at 22:11 with today selected shows:

> **Fully booked** — No slots left on this date — pick another day from the
> strip above.

Nothing is booked. Today's windows (09:00–13:00) simply ended hours ago.

**Cause:** `src/components/SlotPicker.tsx:196-199` (and the date chip `title` at
:142-147) treat *any* empty slot list as "Fully booked". The availability API
returns an unadorned empty list for every reason — cutoff passed, blacked out,
outside the advance window, genuinely full — so the UI cannot tell them apart.

**Why it matters:** it misrepresents supply as scarcity. A customer who reads
"fully booked" on three consecutive evening visits concludes the service is
unavailable in their area, when in fact they just need to book for tomorrow.

**Fix shape:** have the availability response carry a reason for an empty list
and render it ("No slots left today — bookings close at 13:00" vs "Fully
booked" vs "Not available on this date").

---

## 9. Quantity has no upper bound

Holding the **+** button reached quantity **50** with no cap, no warning and no
confirmation. The booking was created and priced at **₹74,950** — fifty deep
cleans into one 4-hour morning window, at one address.

The only server-side check is `Quantity <= 0`
(`BookingSummaryService.cs:49-52`). Slot capacity counts this as **one**
booking regardless of quantity, so quantity does not consume proportional
capacity either.

**Fix shape:** a per-service `MaxQuantity` (validated server-side), and consider
whether capacity should be consumed per unit rather than per booking.

---

## 10. A coupon that stops qualifying dead-ends the whole checkout

**Behaviour:** the classic bargain-hunter path. Customer applies **SAVE20**
(20% off above ₹2500) at quantity 2 — ₹2998 → ₹2398.40, discount shown, all
good. Then they have second thoughts and drop back to quantity 1 (₹1499, below
the minimum).

**What happens:** the entire price panel is replaced by a red error —

> **Couldn't price this booking** — This coupon requires a minimum order amount
> of 2500.00. **[Retry]**

— and **Proceed to book is permanently disabled**. "Retry" re-issues the same
failing request forever. The coupon card still shows SAVE20 as applied, but it
is far below the fold; the customer sees a blocked checkout with no visible
cause and no instruction that removing the coupon is the way out.

**Cause:** `src/app/booking/summary/page.tsx:221` keeps `couponCode` on the
shared summary request, so an invalidated coupon fails the *whole* pricing call
rather than just itself; `disabled={!summary}` on the submit button (line 642)
then blocks checkout. The failure is also framed as a pricing fault rather than
a coupon fault.

**Fix shape:** when the summary call fails specifically on the coupon, drop the
coupon automatically, surface it in the coupon card ("SAVE20 no longer applies
to this order — removed"), and keep the booking priceable.

---

## Summary

| # | Finding | Impact |
|---|---|---|
| 1 | Login wall discards the in-progress booking, lands on `/profile` | Conversion |
| 2 | Impatient taps create multiple real bookings (dead double-submit guard) | Data / trust |
| 3 | Back before paying wipes the draft, then creates a duplicate | Data / trust |
| 4 | Address is never validated against the city/locality being booked | Undeliverable bookings |
| 5 | Already-finished slots are bookable (UTC vs local time comparison) | Undeliverable bookings |
| 6 | Full slots stay visible and pass revalidation, fail at the last click | Conversion |
| 7 | Slot capacity is never released on cancel/refund/abandon/reschedule | Silent loss of supply |
| 8 | "Fully booked" shown for past-cutoff dates | Trust / perceived scarcity |
| 9 | Unbounded quantity (₹74,950 in one slot) | Ops / abuse |
| 10 | An invalidated coupon dead-ends checkout with no way out | Conversion |

Findings 2, 3 and 7 compound: every stray `PaymentPending` booking permanently
consumes a seat that is never returned.

---

## Reproducing

Stack as per `frontend/customer-web/e2e/README.md`. Seeded customer:
`e2e-customer@nestly.local` / `E2eCustomer!Passw0rd`.

Findings 5, 6 and 10 needed catalog data that the seed does not include. Both
fixtures were removed after the pass; recreate with:

```sql
-- Finding 10: a coupon with a minimum order amount
INSERT INTO coupon (id, code, description, discount_type, discount_value,
    max_discount_amount, min_order_amount, valid_from_utc, valid_to_utc,
    is_active, usage_limit_total, usage_limit_per_customer, redemption_count,
    customer_segment, created_at_utc)
VALUES ('22222222-cccc-dddd-eeee-222222222222', 'SAVE20', '20% off above 2500',
    'Percentage', 20, NULL, 2500, now() - interval '1 day',
    now() + interval '30 days', true, NULL, NULL, 0, 'All', now());

-- Findings 5 & 6: a Bengaluru window that has already finished today,
-- plus (for 6) UPDATE slot_window SET max_bookings_per_slot = 1 on it.
INSERT INTO slot_window (id, city_id, name, start_time, end_time, is_active)
VALUES ('11111111-aaaa-bbbb-cccc-111111111111',
    'ba904806-2872-4b4a-a5d4-fed07ba71c97', 'TEMP Late Afternoon',
    '17:00:00', '19:00:00', true);
INSERT INTO slot_window_rule (id, slot_window_id, day_of_week)
SELECT gen_random_uuid(), '11111111-aaaa-bbbb-cccc-111111111111', d
FROM generate_series(0, 6) d;
```

Findings 2 and 3 need the connection throttled (CDP
`Network.emulateNetworkConditions`, ~1200 ms latency) to open the window a real
mobile user sits in; on localhost the route transition is usually fast enough
to hide it.

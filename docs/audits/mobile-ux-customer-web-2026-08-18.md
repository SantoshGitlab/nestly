# Mobile UX audit — customer-web (2026-08-18)

Scope: every route under `frontend/customer-web/src/app`, read against
docs/FRONTEND.md's mobile-first RESPONSIVE DESIGN policy (touch targets,
reachable primary CTAs, mobile keyboard types, modal patterns, table
overflow, safe-area padding, image/perf). Method: static read-through of
every route and the shared components they render, plus targeted greps for
each policy bullet (raw `<input>`/`<Field>` types, `<img>`/`<Image>` usage,
`isPending` loading branches, `role="dialog"`/`<Modal>` usage, `<Table>`
usage). **Not runtime-tested** — no dev server or browser tool was used; this
is a code read, not a device walkthrough. No screenshots exist for the same
reason; each finding below is file:line plus a description of what a phone
viewport (375–430px) would show.

This audit was done as the first step of Phase 22 (tasks #337, #342, #344,
#346, #347, #349, #352, #354, #355) and its findings directly scope the work
in the rest of that batch — each fixed finding below says which task closed
it; open findings say why they were left for a follow-up.

## Findings by route

### `services/[slug]` — service detail (start of the booking funnel)

- **`app/services/[slug]/page.tsx` (aside, "Book now")** — `<aside>` is
  `md:sticky md:top-20`, which only takes effect from `md` up. Below `md`
  (single-column grid) the aside — `PriceCalculator`, `ServiceAvailability`,
  then the "Book now" `LinkButton` — renders *after* the full description,
  two inclusion lists, cancellation/reschedule policy, FAQs and reviews, so
  the button that starts the entire booking flow could sit several screens
  below the fold on a long service. **Fixed under #344** — "Book now" now
  renders inside `StickyActionBar`, unchanged on `md:`.
- Card touch targets (`ServiceCard`, `CategoryTile`) and the accordion-style
  `ServiceFaqs`/inclusion lists were not re-audited individually — they route
  through `Button`/`IconButton`/`Tabs` in `ui.tsx`, already given hit-slop by
  the prior pass on that file.

### Catalog / discovery (`page.tsx` home, `categories`, `categories/[slug]`, `search`)

- **`components/ServiceCard.tsx:51`, `components/CategoryTile.tsx:38`,
  `components/SubcategoryChips.tsx:33`** — admin-supplied photo URLs
  correctly use raw `<img>` (not `next/image` — `next.config.mjs` has no
  `images.remotePatterns`, so an arbitrary admin-uploaded host next/image
  can't be pre-configured for; the existing `eslint-disable-next-line
  @next/next/no-img-element` comments already document this as deliberate),
  but none carried `loading="lazy"`/`decoding="async"`, so a catalog grid of
  a dozen-plus cards — almost all below the fold on a phone — requested every
  image eagerly. **Fixed under #349.**
- `components/HeroBanner.tsx` and `components/PageBanner.tsx` — the only two
  `next/image`/above-the-fold hero images sitewide — already carry correct
  `sizes="100vw"` and `priority` only on the first slide. **Verified, no
  issue**; the "priority applied indiscriminately" failure mode the task
  named does not occur anywhere in this codebase (grep for `priority`/
  `sizes=` across `app`+`components` returns only these two).
- `components/CategoryTiles.tsx`, `app/search/page.tsx`,
  `app/categories/page.tsx`, `app/categories/[slug]/page.tsx` — all already
  render a matching `Skeleton`/`CategoryGridSkeleton` while `isPending`, not
  a blank screen or bare spinner. **Verified under #355, no fix needed.**

### Auth (`login`, `register`, `forgot-password`) and `profile`

- **`components/auth-ui.tsx`'s `OtpField`, used at `login/page.tsx:251,465`,
  `register/page.tsx:212`, `forgot-password/page.tsx:125`** — single wide
  field, deliberately chosen over split boxes for autofill reliability (see
  its own doc comment). Didn't meet #347's "large tap target, auto-advancing"
  ask despite otherwise being solid. **Replaced under #347** by
  `components/OtpInput.tsx` (6 boxes, autofill-safe — see that task's notes
  for the full reasoning and the tradeoff this reverses).
- **`app/profile/page.tsx` (was ~465-481, the mobile/email change OTP box)**
  — hand-duplicated `OtpField`'s exact styling as a plain `Field`, with a
  comment explaining `OtpField` used to strip its `ref` (true at the time —
  `OtpField` has been a `forwardRef` for a while, so the workaround was
  stale). **Fixed under #347** — now shares `OtpInput` like every other OTP
  entry point.
- **`app/login/page.tsx` (`ProviderLoginUnified`)** — accepted 4-8 digit
  provider OTP codes (`providerOtpSchema`), but
  `backend/shared/Infrastructure/Services/ProviderOtpService.cs:52` always
  generates exactly 6 (`GenerateNumericCode(6)`, same as the customer path).
  **Tightened under #347** to match the real 6-digit contract.
- `components/AddressForm.tsx:85` (Pincode) and `:111` (Contact mobile) —
  had `inputMode="numeric"` but no `autoComplete="postal-code"`; `type="tel"`
  but no `autoComplete="tel"`/`inputMode="tel"`. **Fixed under #346.**
  Every other phone/OTP/pincode/email field found by grep
  (`login`, `register`, `forgot-password`, `profile`'s identifier-change
  cards) already carried the correct `type`/`inputMode`/`autoComplete` —
  this was the one gap.
- No editable currency/amount input exists anywhere in customer-web (wallet
  is earn/spend-only, no top-up form) — the "amount fields" bullet of #346
  has nothing to fix.
- `components/LocalitySelector.tsx:42` ("Find your locality") is a combined
  name-or-pincode search, correctly left as a plain text keyboard — it must
  accept letters, a numeric-only `inputMode` would be wrong here.

### Wallet, refer & earn (tables)

- **`app/wallet/page.tsx:223-239`** — the transaction ledger is a 3-column
  `<Table>` (Transaction/Amount/Balance after; the first cell already stacks
  a badge + description + timestamp to stay compact). `Table` wraps in its
  own `overflow-x-auto`, so it can't break the page layout, but three columns
  of financial data is a realistic candidate for within-table horizontal
  scroll on a 375px phone — the exact pattern docs/FRONTEND.md asks tables to
  avoid via a card/list collapse below a breakpoint instead. **Not fixed** —
  out of the nine tasks' explicit scope (none of them mention table layout)
  and redesigning `LedgerRow` as a card is a real UI decision, not a
  mechanical fix; flagging for a follow-up ticket rather than guessing at a
  layout.
- `app/refer-earn/page.tsx:346-353` — a `Friend`/`Status`/`Reward` table
  (three columns, not the two this line originally said), but a much lower
  risk of overflow than the ledger: only one column is numeric and the status
  is a short badge. Not touched, still noted for a follow-up.

> **Update, task 365** — the wallet ledger above **is now fixed**: each entry
> renders as a card below `md` (768px, this app's own mobile/desktop split)
> and as the existing table at `md` and up, one visible at a time, CSS-only.
> The layout decision the audit declined to guess at: the amount stays on the
> entry's own line rather than becoming a label:value pair, because it is what
> a customer opens the screen for, and only "Balance after" carries a visible
> label. `refer-earn` is unchanged and remains the open half of this finding.

### Booking funnel (`booking/summary`, `booking/payment/[id]`, `booking/success/[id]`)

- Already wired to `StickyActionBar` before this batch:
  `booking/summary/page.tsx:1064`, `booking/payment/[id]/page.tsx:334`.
  **Verified, unchanged.**
- `booking/success/[id]/page.tsx` — a short confirmation screen with two
  equal-weight `LinkButton`s ("View booking"/"All bookings"), not a
  scroll-requiring primary CTA. **Left without a `StickyActionBar`**,
  matching the task brief's own suggestion that a confirmation screen may not
  need one.

### Addresses (`addresses`, `addresses/new`, `addresses/[id]/edit`)

- **`components/AddressForm.tsx` submit button** — nine stacked fields
  (label, two address lines, landmark, pincode, city, state, lat/long,
  contact name/mobile, default checkbox) ending in a plain in-flow `Button`.
  `booking/summary/page.tsx:651` links out to `addresses/new` with a
  `returnTo` param when a customer adds a new address mid-checkout, which
  makes `addresses/new` a genuine booking-funnel screen, not only
  account-management furniture. **Fixed under #344** — the shared
  `AddressForm` (used by both `addresses/new` and `addresses/[id]/edit`) now
  renders its submit inside `StickyActionBar`; both pages gained
  `STICKY_BAR_SPACER`. `addresses/[id]/edit` has no `returnTo` handling (only
  reachable from the standalone address book), so it isn't itself part of
  the funnel, but shares the fix rather than forking the form.

### Bookings — detail, reschedule, cancel, review, tracking

- `bookings/[id]/reschedule/page.tsx` — already wired to `StickyActionBar`.
  **Verified, unchanged.**
- **`bookings/[id]/cancel/page.tsx`, `bookings/[id]/review/page.tsx`** —
  neither uses `StickyActionBar`; both are single-purpose forms reached from
  the booking detail page, structurally similar to reschedule. **Not
  fixed** — these are post-purchase servicing screens, not part of "cart/
  summary, address, slot-selection and checkout" (#344's explicit scope);
  flagging as a natural next candidate given reschedule already has the
  treatment, rather than expanding scope unasked.
- **`bookings/[id]/track/page.tsx` (live tracking)** — audited in full under
  #352; see that task's notes. Summary: the map is a modest
  `min-h-[12rem]` inline card, not full-bleed, so it does not trap page
  scroll the way a full-viewport map would; made the Maps SDK's
  `gestureHandling` explicit (`"cooperative"`) rather than relying on its
  implicit default. The "message" control (`ChatWidget`) existed on the
  booking detail page but not here, even though a live job is exactly when a
  customer is most likely to need it — added. "Call" was deliberately left
  alone: the provider's phone number is masked server-side specifically so
  it can't be dialed directly (see `ProviderCard`'s own doc comment), and
  there's no telephony-relay endpoint in this codebase to wire a working
  Call button to — inventing one would either do nothing or fight that
  masking design on purpose.
- `bookings/[id]/page.tsx:209`, `bookings/[id]/track/page.tsx:230` — small
  (32-48px) provider-avatar `<img>`s, both already above the fold on their
  page. Left without `loading="lazy"` — #349 is scoped to catalog/
  service-detail imagery ("the primary discovery surface"), and lazy-loading
  a single always-visible avatar has no real effect either way.

### Dialogs sitewide

- Every dialog found (`app/recurring-bookings/page.tsx`, `app/amc/new/page.tsx`,
  `app/amc/[id]/page.tsx`, `app/subscription/page.tsx`, `app/addresses/page.tsx`,
  `components/CitySelector.tsx`) uses the shared `Modal` from `ui.tsx`, which
  already collapses to a bottom sheet with safe-area padding and
  swipe-to-dismiss below `sm:` (the prior pass on that file).
  `SiteHeader.tsx`'s own dialog is a full-height slide-over drawer, not a
  centered dialog. **No bespoke desktop-style centered modal found anywhere
  in customer-web.**

### Navigation

- Before this batch, phone-width customers reached every one of the app's
  dozen-plus destinations through one hamburger-triggered drawer
  (`SiteHeader.tsx`) — no bottom tab bar existed anywhere in customer-web,
  despite the five most-used destinations (Home/Search/Bookings/Wallet/
  Profile) being exactly what thumb-zone bottom nav is for. **Fixed under
  #342** — new `components/BottomTabBar.tsx`, mounted in `app/layout.tsx`
  alongside `SiteHeader`, hidden on the checkout funnel/standalone-form
  routes where a `StickyActionBar` already claims the bottom of the
  viewport (see that component's own `hideOnRoute` for the exact list and
  reasoning).

### App-wide

- **No web app manifest existed** (`public/manifest.json` absent, no
  `manifest` field on the root `Metadata` export) — "Add to Home Screen" on
  Android had no name/icon/`display: standalone` to install with beyond a
  bare URL. **Fixed under #354.**
- **No offline/poor-connectivity state existed** — `navigator.onLine` and
  the `online`/`offline` window events were unreferenced anywhere in
  customer-web; a customer who lost connectivity got whatever each
  individual failed request happened to render, with no single signal
  telling them the actual cause. **Fixed under #355** — new
  `components/OfflineBanner.tsx`, mounted once in `app/layout.tsx`.
- Skeleton-loading coverage for `isPending` branches was audited across every
  route (`grep` for every `.isPending) {` and `.isPending ? (` block, checked
  for a `Skeleton`/dedicated-skeleton component within the branch) —
  **already consistent everywhere it was checked**, including the booking
  funnel and home/catalog; no blank-screen or bare-spinner offender was
  found. This is a genuinely clean pre-existing state, not a gap this batch
  had to close.

## Not re-verified in a browser

This audit is a static read, per the task's own instructions (no dev
server/backend was started; see the #337/#342/etc. task report for the exact
verification that *was* run — `npm run lint` / `npx tsc --noEmit`). Anything
above stated as "fixed" changed the source in the direction the finding
describes, but has not been visually confirmed at a real 375/390/430px
viewport in a browser.

## Safe-area audit (#351)

Cross-app follow-up to finding 1 in provider-web's audit (same root cause).
Method: same as that pass — static read plus targeted greps for
`fixed`/`sticky` positioning and `env(safe-area-inset-*)` usage across
`frontend/customer-web/src`, no dev server or browser used.

- **`app/layout.tsx`'s `viewport` export had no `viewportFit: "cover"`.**
  Same bug as provider-web's finding 1: `manifest.json` sets `display:
  "standalone"` (task #354), but without `viewport-fit=cover` in the viewport
  meta tag, iOS never opts the page into drawing under the notch/home
  indicator, so every `env(safe-area-inset-*)` reference below — already
  correct on its own — was resolving to `0`. **Fixed** — added
  `viewportFit: "cover"`, matching provider-web's existing pattern.
- **`components/SiteHeader.tsx`'s main bar is `fixed inset-x-0 top-0`** —
  the true top edge, and (unlike admin-web's/provider-web's headers, which
  are `sticky`) genuinely renders full-bleed in standalone-PWA mode with
  nothing above it. **Fixed** — added `pt-[env(safe-area-inset-top)]` to the
  header itself; propagated the resulting height change through the three
  places that hard-coded its old fixed height (`app/layout.tsx`'s `#main`
  spacer, `HeroBanner.tsx`'s cancelling negative margin, and
  `OfflineBanner.tsx`'s `top-[4.5rem]` offset), all now
  `calc(4.5rem+env(safe-area-inset-top))` so they stay in sync. Resolves to
  the original plain `4.5rem` wherever the inset is `0` (every non-notched
  device and every non-standalone context), so this is a no-op change outside
  the scenario it targets.
- **`SiteHeader.tsx`'s mobile menu drawer** (`role="dialog"`, `absolute
  inset-y-0 right-0`) spans the full physical viewport height when open, with
  a `shrink-0` close-button row pinned at its top and a `shrink-0` sign-in/
  out CTA row pinned at its bottom — both genuinely at the screen edges, not
  inside the scrollable middle section. **Fixed** — added
  `pt-[env(safe-area-inset-top)] pb-[env(safe-area-inset-bottom)]` to the
  panel's own outer container (not the fixed-height header/footer rows
  directly, so they don't get squeezed) so the flex column absorbs the extra
  space instead.
- **`BottomTabBar.tsx`, `patterns.tsx`'s `StickyActionBar`, `ui.tsx`'s
  `Modal` bottom-sheet** — already correct
  (`env(safe-area-inset-bottom)`/`max(...)` clamps present before this task).
  **Verified, no change.**
- **`ui.tsx`'s toast container** (`fixed inset-x-0 bottom-0`) — not in the
  task's named list but surfaced by the `fixed`/`sticky` grep sweep. Sits at
  the true bottom edge with a flat `p-4`, so a toast could land directly over
  the iPhone home-indicator gesture area — this inset is non-zero even in an
  ordinary browser tab (not only standalone-PWA mode), unlike the top inset,
  so this was worth fixing regardless of `viewportFit`. **Fixed** — added
  `supports-[padding:max(0px)]:pb-[max(1rem,env(safe-area-inset-bottom))]`,
  keeping the existing 1rem as a floor.
- **`OfflineBanner.tsx`** — `fixed inset-x-0 top-[4.5rem]`, sitting below the
  header rather than at the true top edge itself. **Judgment call: no
  `safe-area-inset-top` needed on the banner itself** — only its numeric
  offset needed updating to track the header's new (possibly taller) height,
  which the `calc()` change above already covers.
- **`app/services/[slug]/page.tsx`, `booking/summary/page.tsx`,
  `booking/payment/[id]/page.tsx`, `bookings/[id]/page.tsx`'s `aside`
  elements** (`md:sticky md:top-20`) — sticky only from `md` up, and `top-20`
  is an offset below the header, not the true viewport edge. **Judgment
  call: no change** — matches the task's own example of a sticky element
  that doesn't need safe-area padding.
- Baseline `width=device-width, initial-scale=1` viewport meta — verified by
  reading Next.js 14.2.35's own `createDefaultViewport`/`mergeViewport`
  source (`node_modules/next/dist/lib/metadata/`) rather than assuming: Next
  merges these defaults into the resolved viewport regardless of what the
  `viewport` export sets, so this was already correct with no code change
  needed.

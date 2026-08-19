# Mobile UX audit — provider-web

Phase 22 (Mobile-First Experience), task #338. Scopes the provider-web-side
work on #343, #345, #346, #347, #352, #354, #355.

**Methodology:** a static read-through of every route under
`frontend/provider-web/src/app`, reasoning explicitly about a 375px-wide
viewport (iPhone SE/mini class — the tightest common width) and about
one-handed, in-field use: for a phone held in one hand with the thumb doing
all the tapping, the **bottom third of a tall screen is easy to reach; the top
third is not** without a grip shift. **No dev server, backend or browser was
run for this pass** — findings below are inferred from the component tree,
existing Tailwind classes, and the in-code design-rationale comments already
present (this codebase is unusually good about explaining *why* a layout
choice was made, which is quoted where it changes the verdict). Anything that
would need a live render to confirm (exact pixel wrapping, real device safe
area insets) is flagged as such rather than asserted.

Every numbered finding below was acted on in this same task batch (fixed, or
explicitly deferred with a reason) — see the per-finding "Action" line.

---

## Global chrome

**1. `env(safe-area-inset-*)` was silently inert everywhere it's used.**
`ProviderTabBar` (`components/ProviderSidebar.tsx`, `pb-[env(safe-area-inset-bottom)]`)
and `ui.tsx`'s `Modal` bottom-sheet (`pb-[env(safe-area-inset-bottom)]`, added
just before this task) both reference the safe-area inset, but the root
`viewport` export (`app/layout.tsx`) never set `viewport-fit: cover`. Without
that, browsers don't opt the page into drawing under the notch/home
indicator, and `env(safe-area-inset-*)` resolves to `0` — the existing
safe-area code was shipping but doing nothing on an actual notched phone.
**Action: fixed** — added `viewportFit: "cover"` to `app/layout.tsx`'s
`viewport` export. This also makes the new `StickyActionBar`'s (#345) own
safe-area padding work correctly as a home-screen PWA (#354).

**2. No offline/connectivity signal anywhere.** grep for
`navigator.onLine`/`online`/`offline` across the app returned nothing. A
provider on a job site with one bar of signal gets no indication that a
mutation is failing for that reason rather than a real error — exactly the
scenario #355 calls out. **Action: fixed** — `components/OfflineBanner.tsx`,
mounted near the root of both the authenticated shell
(`app/(provider)/layout.tsx`) and the auth screens (`components/auth-ui.tsx`'s
`AuthShell`).

**3. Skeleton loading-state coverage is already thorough.** Every screen/
section backed by `useQuery` (job list, job detail, both earnings sub-pages,
every profile section, both availability sections) ships a `Skeleton`/
`SkeletonText` shaped to match its real content — verified by cross-checking
every `useQuery` call site against `Skeleton` usage in the same file. **Action:
none needed** — the loading-state half of #355 was already in good shape;
the real gap was finding 2 above.

---

## `/login`, `/register`

**4. OTP entry was a single generic text field**
(`components/auth-ui.tsx`'s old `OtpField`), not the dedicated boxed
component #347 asks for. The field itself was already well-optimised for
one-handed autofill (see that component's own since-removed rationale
comment) — the gap was purely that it didn't look like the "enter a code"
affordance users expect. **Action: fixed** — `components/OtpInput.tsx`,
wired into both `app/login/page.tsx` and `app/register/page.tsx`; see that
file's header comment for why it keeps the single-real-`<input>` mechanism
underneath boxed visuals rather than switching to `length` independently
focusable inputs.

**5. Mobile field (`app/login/page.tsx:190`, `app/register/page.tsx:154`)**
already has `type="tel" inputMode="tel" autoComplete="tel"`. **No change
needed.**

**6. Register's email field (`app/register/page.tsx:~198`)** had `type="email"`
and `autoComplete="email"` but was missing `inputMode="email"` — present on
the equivalent field in `ProfileDetailsSection.tsx` but not here.
**Action: fixed** (#346) — one-line addition, `inputMode` is otherwise
redundant with `type="email"` on most engines but keeps the two forms
consistent rather than silently different.

**7. Primary CTAs** (`Send verification code` / `Sign in` / `Complete
registration`) are already `size="lg" fullWidth`, sitting directly under a
short field stack — no scroll-to-reach issue; these forms are short enough
that a sticky bar would be over-engineering. **No change needed.**

---

## `/jobs` (job list)

**8. Card-based list, not a table** (`app/(provider)/jobs/page.tsx`) — the
file's own comment already states the reasoning ("the fields that decide
whether to accept a job ... do not fit a phone-width table"). Each `JobCard`
is itself the tap target (`jobs/page.tsx:178-234`), not a small "view" link
in the corner — good one-handed target sizing. **No change needed.**

**9. Filters card sits above the list** (`jobs/page.tsx:82-115`) — acceptable:
it's a secondary, occasional control, and the primary repeating action (tap a
job) is the thing that scrolls into thumb reach as the list is browsed. Not
flagged as a one-handed problem.

---

## `/jobs/[id]` (job detail) — the primary field-use screen

**10. Accept/Decline sits at the very top of the page, not the bottom, and
was deliberately left that way.** The existing comment on this block
(`jobs/[id]/page.tsx`, "The decision, before anything else on the page")
explains why: it's the single most consequential, time-boxed action on the
screen, so it must be the first thing rendered — not something scrolled to,
top *or* bottom. Converting it to the new bottom `StickyActionBar` would
contradict that rationale (it would then compete with, not lead, the rest of
the page). **Action: kept as-is — see "Judgment calls" in the task report.**

**11. Every *other* primary action was a plain inline button at the bottom of
a long, scrollable page** — "On my way"/"I've arrived"/"Start job" (`Ready to
go?` card), "Mark complete" (`Finishing up` card), and "Submit verification"
(bottom of the photo+checklist form, itself growable as photos/checklist rows
are added). None were reachable without scrolling once the job details, chat
thread, and location-sharing card above them pushed the page past one
screen — exactly what #345 was opened for. **Action: fixed** — all three now
render inside `StickyActionBar` (`components/patterns.tsx`) whenever they are
the screen's current actionable step; see the task report's "Judgment calls"
for how the two InProgress-state candidates (submit verification vs. mark
complete) are kept mutually exclusive so only one bar is ever pinned at once.

**12. No "navigate to address" affordance.** The address is fully available
(`addressLine1Snapshot` … `addressPincodeSnapshot`,
`jobs/[id]/page.tsx:351-360`) but there is no maps deep link — only a `tel:`
call link exists as an external-app handoff. For a provider actually driving
to a job, this is a real gap. **Action: not fixed** — #352 asks to verify
existing controls' reachability, not add a new one; adding a maps link is a
feature addition outside a "mobile UX pass" scope call. Flagged here for a
follow-up task.

**13. No map is ever rendered provider-side.** `useLocationSharing.ts` only
*sends* `watchPosition` fixes to the ingest endpoint in the background; it
never receives or renders anything (confirmed against `docs/TRACKING.md`).
#352's "the map doesn't trap page scroll" concern is **not applicable** to
provider-web — there is no map to trap it. The `LocationSharingCard` is a
static status card with no interactive controls of its own.

**14. Call-customer link** (`jobs/[id]/page.tsx:331-348`, inside the "Job
details" card) sits high on the page, not thumb-reachable without scrolling
back up once further down the page. Deprioritized: it's an occasional,
secondary action (unlike Accept/Start/Complete, nothing blocks on it), and
promoting it to the sticky bar would crowd out the actual primary action for
each state. **Action: not fixed**, noted for awareness.

**15. Bottom tab bar and the new sticky action bar would otherwise both be
fixed to the viewport bottom simultaneously on this one route** — see #343
below. **Action: fixed** (tab bar now hides on `/jobs/{id}`).

**16. Photo capture already uses `capture="environment"`**
(`jobs/[id]/page.tsx:824-835`) for direct rear-camera access on a phone
browser, with `multiple` so one tap can add several shots. Already
mobile-first. **No change needed.**

---

## `/availability`

**17. Day-of-week toggle's tap target is the whole row, not just the switch**
(`WindowsSection.tsx:230-233`) — already documented in-code as intentional
one-handed design. **No change needed.**

**18. Blackout date range auto-fills "To" from "From"**
(`BlackoutDatesSection.tsx:163-181`) to save a second date-picker interaction
on a phone. Already mobile-first. **No change needed.**

---

## `/earnings`, `/earnings/payouts/[id]`

**19. Fully read-only** — no forms, no keyboard-type concerns. Each section
(`SummarySection`, `LedgerSection`, `PayoutsSection`) queries and fails
independently, which matters for a patchy-network screen: a slow ledger
fetch doesn't blank the balance number that already loaded. **No change
needed.**

---

## `/profile`

**20. `ProfileDetailsSection`'s email field already had the correct
`type="email" inputMode="email"`** — the baseline the register page (finding
6) was missing. **No change needed here.**

**21. KYC/photo "URL or reference" text fields**
(`KycSection.tsx:181-193`, `PhotoSection.tsx:153-158`) and the job detail
page's legacy proof-ref field (`jobs/[id]/page.tsx:~530`) are plain
`type="text"`, not `type="url"`. Left alone: #346's brief names phone/OTP/
pincode/email/amount specifically, and none of these fields are pure URLs
(two are explicitly "URL *or* reference ID"), so `type="url"`'s stricter
mobile keyboard (adds `.com`/`/` keys, but browsers also validate/style
`:invalid` differently for `type=url`) is a closer call than a clear win.
**Action: not fixed**, flagged rather than guessed at.

**22. `ServiceAreasSection`'s city/zone/pincode are all `<Select>` dropdowns,
not text inputs** — confirmed no pincode text field exists anywhere in
provider-web to apply a numeric keyboard type to. **No change needed.**

---

## #343 verification — bottom tab bar

Re-derived the actual rendered tap-target height rather than trusting the
column math: at `grid-cols-4` each tab is `flex flex-col items-center gap-1
px-1 py-2.5` around a `h-5 w-5` icon (20px) + `gap-1` (4px) + `text-[0.6875rem]`
label (~13px line height) = ~37px of content, plus `py-2.5` (10px × 2) = **~57px
tall** — clears the 44px minimum with room to spare, independent of the 44px
hit-slop convention used elsewhere in `ui.tsx`. Column width at 375px ÷ 4 =
~94px, ample for a 5-character label ("Jobs", "Earnings", "Profile",
"Availability" truncates but the icon alone still identifies the tab) — not
cramped. **Verified correct as shipped, no change to sizing.**

The one real gap: it did not coordinate with the new `StickyActionBar`
(#345) — both are `fixed inset-x-0 bottom-0`, and job detail is the one
screen that needs the sticky bar. **Action: fixed** — `ProviderTabBar` now
returns `null` on `/jobs/{id}` (`isJobDetailPath`,
`components/ProviderSidebar.tsx`), matching how a native app hides its
bottom tabs on a task-focused detail screen. The authenticated layout's main
content padding is route-aware to match (`STICKY_BAR_SPACER` on job detail,
the tab bar's `pb-24` everywhere else).

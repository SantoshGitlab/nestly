# admin-web mobile/tablet usability audit (task #339)

Date: 2026-08-18
Scope: admin-web only, evaluated at the ~768px tablet viewport defined by
docs/FRONTEND.md's RESPONSIVE DESIGN section — "stays usable on a tablet for
on-the-go checks (approve a provider, look up a booking) without horizontal
scrolling or broken layout". admin-web is desk-first, not mobile-first: this
audit does **not** flag anything that only breaks below tablet width (~375px
phone), and does not recommend a bottom tab bar or phone-first rework.

**Method**: static read of every route under `src/app/(admin)/` plus the
shared components they render through (`DataTable`, `EntityTable`,
`AdminSidebar`, `FormGrid`, `DescriptionList`). No dev server was started and
nothing was verified in a real browser — findings below are derived from
Tailwind breakpoints and explicit pixel widths in the source (`minWidth=`,
`min-w-[…]`, `grid-cols-N` without a responsive override), which is
deterministic enough to identify horizontal-scroll and broken-grid cases
without a running app. Treat this as a code-level audit, not a rendered
screenshot pass.

## Summary

The one systemic issue is **dense `DataTable`/`EntityTable` instances**:
every list screen in admin-web is built on the shared `DataTable` component
(`src/components/data-table.tsx`), which renders a single `<table>` with an
explicit `minWidth` and wraps it in `overflow-x-auto`. At a 768px viewport
(main content area is narrower still once the ~256px sidebar is accounted
for — though the sidebar itself already collapses to a drawer below `lg`/
1024px, see below) any table whose `minWidth` exceeds the available width
forces horizontal scroll to see trailing columns, which is exactly what
docs/FRONTEND.md prohibits for the tablet floor.

Nearly every other admin-web surface (nav, detail pages, forms, stat grids)
already degrades gracefully at 768px because it's built from Tailwind's
responsive grid utilities (`sm:grid-cols-2 lg:grid-cols-3`, etc.) rather than
fixed widths, or already treats `lg` (1024px) as its own mobile/tablet
breakpoint (the sidebar drawer). Those are noted as "OK" below so the
scoping for #348 is grounded in what actually breaks, not assumed.

## Routes an on-the-go admin actually uses (priority order per the task)

### Bookings list — `src/app/(admin)/bookings/page.tsx`
- `src/app/(admin)/bookings/page.tsx:226` — `DataTable minWidth="920px"`,
  7 columns (Customer, Service, City, Slot date, Status, Total, Created) +
  a row-actions column. **Breaks at 768px**: forces horizontal scroll to
  reach Status/Total/Created/Actions, which is exactly the "look up a
  booking" on-the-go case named in the task. **In scope for #348.**
- `src/app/(admin)/bookings/recurring-plans/page.tsx:322` — `minWidth="880px"`,
  same failure mode, lower traffic than the main list. Flagged but not
  prioritized (not one of the task's named routes).
- `src/app/(admin)/bookings/conflicts/page.tsx:117` — plain `sm:grid-cols-2`
  card grid, no fixed-width table. OK at 768px.

### Booking detail — `src/app/(admin)/bookings/[bookingId]/page.tsx`
- Uses `DescriptionList`/`FormGrid` (`sm:grid-cols-2 lg:grid-cols-3`), no
  `min-w-[…]` or unresponsive `grid-cols-3+`. At 768px (below `lg`) these
  fall back to 2 columns, which reads fine. **OK — no changes needed.**

### Providers list — `src/app/(admin)/providers/page.tsx`
- `src/app/(admin)/providers/page.tsx:208` — `DataTable minWidth="920px"`.
  Same shape as bookings. **Breaks at 768px** — this is the literal
  "approve a provider" on-the-go case from the task. **In scope for #348.**

### Provider detail (approval) — `src/app/(admin)/providers/[providerId]/page.tsx`
- Only responsive grids (`grid-cols-1 sm:grid-cols-2 lg:grid-cols-3`), no
  fixed-width elements. **OK — no changes needed.** The approve/reject
  action controls are regular buttons in the normal flow, not something
  that scrolls off-screen.

### Customers list — `src/app/(admin)/customers/page.tsx`
- `src/app/(admin)/customers/page.tsx:183` — `DataTable minWidth="920px"`,
  7 columns (Name, Mobile, Email, City, Status, Bookings, Registered).
  **Breaks at 768px.** **In scope for #348.**

### Customer detail — `src/app/(admin)/customers/[customerId]/page.tsx`
- Responsive grids only. **OK.**

### Payments list — `src/app/(admin)/payments/page.tsx`
- `src/app/(admin)/payments/page.tsx:199` — `DataTable minWidth="920px"`,
  6 columns (Transaction, Status, Amount, Gateway reference, Created,
  Updated). **Breaks at 768px.** **In scope for #348.**

### Payment (transaction) detail — `src/app/(admin)/payments/[transactionId]/page.tsx`
- Responsive grids only. **OK.**

## Every other `DataTable`/`EntityTable` instance, by `minWidth`

Collected so #348 doesn't have to re-derive this list. `lg` (1024px) is
the breakpoint already used elsewhere in admin-web for the tablet/desktop
split (see "Navigation" below), so anything with `minWidth` above the
content area available under 1024px is a candidate; anything comfortably
under ~700px is very unlikely to force scroll even accounting for page
padding.

| minWidth | File |
|---|---|
| 420–680px | reports, StatesSection, CutoffsSection, AddOnPricesSection, CitiesSection, PincodesSection, ZonesSection, CategoryCityMappingSection, ServicePincodeMappingSection — all comfortably narrow, **OK** |
| 720–820px | admin-users/roles, chat, CityPricingPolicySection, referral/config, LocalitiesSection, catalog, catalog/service-groups, catalog/addon-groups, dashboard, CityPricesSection, BlackoutsSection — borderline; likely fine on a 768px *browser* viewport once page gutters are subtracted, but tight. Not in the task's named list; not touched by #348, but candidates for a future pass. |
| 860–920px | referral, admin-users, amc/renewal-report, bookings/recurring-plans, PromotionalPricesSection, ExportQueueCard, OverridesSection, catalog/addons, coupons/redemptions, AmcPlansTable, amc/contracts, **bookings, customers, payments**, providers | Above the tablet floor — will scroll. The bold four are this audit's priority set and are fixed in #348. |
| 960–1240px | catalog/services, CmsFaqsTable, CmsPagesTable, subscription-plans/PlansTable, slots/WindowsSection, audit, BannersTable, NotificationTemplatesTable, support, CouponsTable | Clearly break at 768px, same root cause. |

**Every one of these renders through the shared `DataTable` component**
(`src/components/data-table.tsx`) or `EntityTable`
(`src/components/entity-table.tsx`, itself built on `DataTable`) — see
"Files Modified" in the #348 write-up for why fixing it once there covers
all of them.

## Non-table checks

- **Navigation** (`src/app/(admin)/layout.tsx:19,60,65`) — sidebar is
  permanent from `lg` (1024px) up and becomes a slide-over drawer below it,
  already documented in that file's own comment. At 768px this is a drawer
  with a visible open/close control. **OK, no phone-first bottom-tab
  behavior needed or added**, matches the desk-first policy explicitly.
- **Modals** (`src/components/ui.tsx`) — already updated by a prior agent
  for bottom-sheet/touch-target polish per this task's brief; not
  re-audited here.
- **Forms** (`FormGrid`, `Field`, etc.) — all `sm:grid-cols-2 lg:grid-cols-3`
  patterns, degrade to 1–2 columns at 768px. **OK.**
- **Role permission matrix** — `src/app/(admin)/admin-users/roles/[roleId]/page.tsx:229`
  is the one screen with a **hand-rolled `<table>`** (not on `DataTable`):
  `min-w-[480px]`, 3 columns (Module, Read, Write). Well under the tablet
  floor — **OK, no fix needed.** Noted for #348 as the one inconsistency:
  it doesn't use the shared primitive, but it doesn't need the card
  treatment either since it never forces scroll at 768px.
  **Update, task 367 — migrated to `DataTable` anyway.** The audit's reading
  holds (it never broke at 768px, and this changed no behaviour that was
  broken); what closed it was the inconsistency itself, since being the one
  exception meant every future table fix had to be made twice. Every
  admin-web table now goes through one already-responsive primitive.

## Findings not fixed here, with why

- The 720–820px "borderline" table group above is not in the task's named
  routes (bookings/customers/payments/providers) and the task explicitly
  says to find the real offending list rather than rework every table
  speculatively. Once #348's shared-component fix lands, these get the
  same card layout for free (see #348 report) since they go through the
  same `DataTable`, so this is really scoping, not a gap.
- Dashboard/report widgets, stat tiles, and chart cards were skimmed but
  not exhaustively walked screen-by-screen (out of the four priority
  routes); nothing wide-fixed (`min-w-[…]`) turned up in the grep sweep
  across `src/app/(admin)/**`, so no further action taken.

## Safe-area audit (#351)

Cross-app follow-up. Method: same as the rest of this audit — static read,
targeted greps for `fixed`/`sticky` and `env(safe-area-inset-*)` across
`frontend/admin-web/src`, no dev server/browser used.

- **`app/layout.tsx`'s `viewport` export had no `viewportFit: "cover"`.**
  admin-web added no bottom nav/sticky CTA this phase (desk-first per
  policy, confirmed by this same audit's "Navigation" section above), but
  `ui.tsx`'s `Modal` still carries a bottom-sheet mobile state with
  `env(safe-area-inset-bottom)` padding from earlier this phase, which
  needed this to actually take effect on a phone-width admin session.
  **Fixed** — added `viewportFit: "cover"`, matching the pattern already
  shipped in provider-web/customer-web.
- **`components/AdminHeader.tsx`** (`sticky top-0`) and the mobile sidebar
  drawer (`app/(admin)/layout.tsx`, `fixed inset-0 lg:hidden` wrapping
  `AdminSidebar`) both sit at the true top edge. **Judgment call: no
  `safe-area-inset-top` added to either.** admin-web has no
  `public/manifest.json` (confirmed absent — grep for `manifest` in
  `app/layout.tsx` and `ls public/` both come back empty), so it cannot
  launch in standalone/full-bleed display mode; "Add to Home Screen" on iOS
  without a manifest or `apple-mobile-web-app-capable` meta opens as a
  normal Safari tab with its own chrome above the page, which already covers
  a notch/punch-hole camera. Regular in-tab browsing is the only context
  this app runs in, so there is no scenario where these top-edge elements
  render under a notch. `AdminSidebar`'s own content (`nav`, `overflow-y-auto`,
  `p-4`) has no header/footer row of its own pinned outside the scrollable
  area, so even a hypothetical standalone mode would have nothing to fix
  there beyond the drawer wrapper.
- **`components/data-table.tsx`'s `sticky top-0`** (table header, gated on
  `maxHeight`) — sticky *inside* a bounded, independently-scrollable table
  container, not at the true viewport edge. **Judgment call: no change** —
  matches the task's own example of a sticky element that doesn't need
  safe-area padding.
- **`ui.tsx`'s toast container** (`fixed inset-x-0 bottom-0`) — flat `p-4`
  at the true bottom edge. Unlike the top inset, the bottom inset is
  non-zero on iPhone X+ even in an ordinary browser tab (accounts for the
  home-indicator gesture area, not just standalone mode), so this applies
  regardless of admin-web having no manifest. **Fixed** — added
  `supports-[padding:max(0px)]:pb-[max(1rem,env(safe-area-inset-bottom))]`,
  matching the same fix made in customer-web and provider-web.
- **`ui.tsx`'s `Modal` bottom-sheet** — already correct
  (`pb-[env(safe-area-inset-bottom)]`, from earlier this phase). **Verified,
  no change** — now actually takes effect once `viewportFit: "cover"`
  activates it on a phone-width session.
- Baseline `width=device-width, initial-scale=1` viewport meta — verified by
  reading Next.js 14.2.35's own `createDefaultViewport`/`mergeViewport`
  source (`node_modules/next/dist/lib/metadata/`): Next merges these
  defaults into the resolved viewport regardless of what the `viewport`
  export sets, so this needed no change.

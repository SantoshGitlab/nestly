# TRACKING.md — End-to-end order tracking (Phase 16)

Owning document for live order tracking: the fulfilment state machine's
tracking states, provider location ingest, ETA computation, the SignalR
tracking hub, and the Google Maps configuration surface all of that depends
on. See `docs/README.md` for how this fits the rest of the documentation
suite.

## 1. State machine

`Nestly.Domain.BookingLifecycle` is the single authority on which
`BookingStatus` transitions are legal — the tracking hub, the ingest
endpoint, and the tracking read model all call `BookingLifecycle.IsTrackable`
rather than keeping their own copy of the trackable-status set, specifically
so they cannot drift into disagreeing about when tracking exists.

Full transition table:

```
Initiated           -> PaymentPending, CancelledByCustomer
PaymentPending       -> Confirmed, PaymentFailed, CancelledByCustomer, Expired
PaymentFailed        -> PaymentPending, CancelledByCustomer, CancelledByAdmin
Confirmed            -> AwaitingFulfilment, Rescheduled, CancelledByCustomer, CancelledByAdmin
AwaitingFulfilment   -> Assigned, Rescheduled, CancelledByCustomer, CancelledByAdmin
Assigned             -> ProviderEnRoute, InProgress, AwaitingFulfilment, Rescheduled, CancelledByCustomer, CancelledByAdmin
ProviderEnRoute      -> ProviderArrived, Rescheduled, CancelledByCustomer, CancelledByAdmin
ProviderArrived      -> InProgress, Rescheduled, CancelledByCustomer, CancelledByAdmin
InProgress           -> Completed, CancelledByAdmin
Completed            -> RefundPending
CancelledByCustomer  -> RefundPending, Refunded
CancelledByAdmin     -> RefundPending, Refunded
Rescheduled          -> AwaitingFulfilment, CancelledByCustomer, CancelledByAdmin
RefundPending        -> Refunded
Refunded             -> (terminal)
Expired              -> (terminal)
```

**Trackable statuses** (`BookingLifecycle.IsTrackable`): `Assigned`,
`ProviderEnRoute`, `ProviderArrived`, `InProgress`. Everything before
`Assigned` has no provider to track; everything after `InProgress`
(completed, cancelled, refunded) has nothing left to watch.

### Why `Assigned -> InProgress` stays valid

`ProviderEnRoute` and `ProviderArrived` are both optional steps, not
mandatory gates. Tapping "on my way" is optional, and a provider who goes
straight from accepting a job to starting work must not be blocked by a
tracking state they never entered — so `Assigned -> InProgress` is kept
alongside the two tracking states rather than removed once they existed.

The two tracking states carry the same cancel/reschedule edges `Assigned`
has (a customer whose provider is en route or at the door has not lost the
right to cancel, and admin can still step in), but deliberately **not**
`Assigned`'s `AwaitingFulfilment` edge — that edge exists for a provider
rejecting an offer, and a provider who has already set off has accepted the
job. Returning the booking to the assignable pool from `ProviderEnRoute` or
`ProviderArrived` is a withdrawal/reassignment, not a rejection, and is not
modeled as a lifecycle transition here.

One further asymmetry worth knowing: `ProviderEnRoute -> InProgress` does
**not** exist. Once a provider has tapped "on my way", they must also tap
"arrived" before starting — only the *fully skipped* path
(`Assigned -> InProgress`) is legal, not a partial skip from mid-journey.
provider-web's job detail screen enforces this in its own UI (hides the
Start button while `EnRoute`, shows it again once `Arrived`) rather than
relying on the backend to reject it silently.

## 2. Location ingest: authorization, throttle, retention

`POST /api/v1/jobs/{bookingId}/location` (provider-api,
`ProviderLocationIngestService.RecordAsync`).

**Authorization** (fail-closed, in order):
1. Caller must be the provider on the booking's *live* assignment
   (`Assigned` or `Accepted` status) — not merely a provider who once held
   it.
2. The assignment must specifically be `Accepted` (not just offered).
3. The booking must be in a trackable status (`BookingLifecycle.IsTrackable`).

**Validation**: latitude/longitude bounds, and a device-clock contract —
`recordedAtUtc` may not be more than `FutureSkewToleranceSeconds` (30s) in
the future, and a fix older than `MaximumAgeMinutes` (5) is rejected as
stale.

**Client-side throttle** (`ProviderLocationIngestOptions`, section
`"ProviderLocationIngest"`):

| Setting | Default | Range | Meaning |
|---|---|---|---|
| `MinimumIntervalSeconds` | 15 | 0–3600 | Minimum gap between accepted fixes for one booking. Roughly the resolution a moving-vehicle marker needs to look live without jumping; caps one job at ~240 rows/hour. `0` disables throttling — for load tests only, never production. |
| `MaximumAgeMinutes` | 5 | 1–60 | How stale a fix may be and still be accepted; also bounds the burst-catch-up window (`MaximumAgeMinutes / MinimumIntervalSeconds` ≈ 20 rows by default). |
| `FutureSkewToleranceSeconds` | 30 | 0–300 | How far into the future a device clock may claim a fix was recorded. Small enough that it cannot be used to fabricate a plausible future trail. |

The throttle is scoped **per booking, not per provider** — a provider
running two jobs cannot exhaust one booking's throttle window by pinging
the other. It is a read-then-write comparison against the latest stored
ping, not an atomic reservation: two concurrent requests can both pass it in
principle. This is an accepted, documented limitation bounded by request
concurrency, not by client chattiness — a chatty client is still held to
one row per interval.

A throttled fix is **not an error**: the endpoint returns 202 with
`Accepted: false` and a `NextAcceptedAfterUtc` hint, not a 4xx/5xx.

### Retention — no pruning job exists yet

`ProviderLocationPing`'s own doc comment states the intended policy: this is
operational data, not permanent history. A trail exists to drive a live
tracking screen and to settle a dispute shortly afterwards; once a booking
closes and passes its dispute window, its pings should be pruned, and pings
never attached to a booking should be pruned sooner still. A permanent
minute-by-minute movement history of a worker is a privacy liability with no
operational payoff, and this data is explicitly not an audit log
(`AuditLog` is the audit log).

**As implemented today, no such pruning job exists.** Location pings
accumulate in the `provider_location_ping` table indefinitely. This is a
real gap, not a documentation oversight — track it as follow-up work in
`tasks.csv` before this feature carries meaningful production traffic. Until
a pruning job ships, be aware that this table has no natural ceiling.

## 3. ETA pipeline and cost profile

`BookingEtaService.RefreshAsync` runs after every accepted location ping
and after `MarkEnRouteAsync`. It never throws to its caller — every
exception except caller cancellation is caught, logged at Warning, and
swallowed, because an ETA is a nice-to-have overlay, not something a ping or
a status transition should fail over.

Flow: if the booking is no longer trackable, the ETA is cleared (not left
stale). Otherwise it requires at least one stored location ping — with none,
`RefreshAsync` no-ops rather than computing a route from a stale coordinate
left over from a different job. If a ping exists, `BookingTracking.ShouldRecompute`
gates the actual route lookup.

**ETA-specific throttle** (`BookingEtaOptions`, section `"BookingEta"`,
framed as a billing control, not a tuning preference):

| Setting | Default | Range | Meaning |
|---|---|---|---|
| `MinimumRecomputeIntervalSeconds` | 60 | 0–3600 | Cuts the worst case from 240 lookups/hour/job (matching the ingest throttle) down to 60. |
| `MinimumMovementMetres` | 250 | 1–100,000 | Roughly a city block — GPS scatter from a stationary/parked provider cannot trip a recompute. |

**Suppression outside trackable states**: leaving the trackable window
(completion, cancellation, etc.) clears the stored ETA rather than leaving
the last number displayed as if it were still live.

### Two `IRouteEstimateProvider` implementations

- **`GoogleMapsRouteEstimateProvider`** (`backend/shared/Infrastructure/Services/GoogleMapsRouteEstimateProvider.cs`)
  — real, billed calls to Google Maps Platform's **Routes API**
  (`computeRouteMatrix`, `TravelMode=DRIVE`, `RoutingPreference=TRAFFIC_AWARE`).
  Deliberately the Routes API and not the legacy Distance Matrix API, which
  went legacy on 2025-03-01 and cannot be enabled on a new Cloud project —
  do not "fix" this back to Distance Matrix. The key is sent only via the
  `X-Goog-Api-Key` header, never in a URL, log line, or cache key.
  Per-leg results are cached (TTL below); destinations are batched, and any
  destination that fails or falls outside a batch is individually filled
  from the sandbox estimator rather than failing the whole request.
- **`SandboxRouteEstimateProvider`** (`backend/shared/Infrastructure/Services/SandboxRouteEstimateProvider.cs`)
  — free, local, deterministic: great-circle distance (`GeoDistance.MetresBetween`,
  Haversine) × a road-winding factor ÷ an average speed (floor 1 kph). No
  network call, no credentials, no cost.

**When the sandbox is used**: it is not only a failure fallback. At
startup, `RouteEstimateRegistration` binds `IRouteEstimateProvider` to
`GoogleMapsRouteEstimateProvider` **only if** `GoogleMapsOptions.IsConfigured`
(`Enabled == true` and `ApiKey` non-empty); otherwise the whole binding is
the sandbox provider. So local development and CI, which have no Maps
billing account, run entirely on the sandbox by default — nothing needs to
be mocked or flagged for that to happen. Separately, even when the real
provider is bound, any single-destination failure inside it degrades to a
sandbox estimate for that destination and logs a warning, rather than
failing the whole route computation.

### `GoogleMapsOptions` (section `"GoogleMaps"`)

| Setting | Default | Range | Meaning |
|---|---|---|---|
| `ApiKey` | *(none)* | — | Server-side key. Absent → the whole integration falls back to the sandbox. Never in source or an appsettings file — see §5. |
| `Enabled` | `true` | — | Kill switch. `false` forces the sandbox estimator even when a key is present. |
| `TimeoutSeconds` | 5 | 1–30 | Per-call HTTP timeout. |
| `CacheTtlSeconds` | 60 | 0–3600 | Per-leg cache TTL — short by design, since traffic-aware estimates go stale quickly. `0` disables caching. |
| `MaxDestinationsPerCall` | 25 | 1–100 | Batch size per Routes API call. |
| `MaxDestinationsPerEstimate` | 100 | 1–500 | Hard cost ceiling per estimate request; anything past this gets a sandbox estimate and a logged warning rather than another billed call. |
| `MaxElementsPerRequestLimit` | 100 (const) | — | Google's own ceiling for `TRAFFIC_AWARE`/`TRAFFIC_AWARE_OPTIMAL`/`TRANSIT` routing preferences (higher, 625, only applies to `TRAFFIC_UNAWARE`, which this integration does not use). |

## 4. Auto-assignment's use of routing (`AutoAssignmentOptions`, section `"AutoAssignment"`)

Auto-assignment ranks candidate providers by real road travel time using the
same `IRouteEstimateProvider` seam — see `docs/PROVIDER.md`/`ARCHITECTURE.md`
for the assignment flow itself; only the routing-related switches are
documented here since they share the Maps cost profile above.

| Setting | Default | Range | Meaning |
|---|---|---|---|
| `Enabled` | `true` | — | Master kill switch for auto-assignment; `false` falls back to fully manual admin assignment. |
| `RetryAttempts` | 3 | 0–20 | |
| `RouteRankingEnabled` | `true` | — | Kill switch. `false` ranks candidates by straight-line distance only — no route call issued at all. |
| `MaxRouteCandidates` | 10 | 1–50 | How many nearby candidates get a real route lookup. |
| `RouteRankingRadiusKm` | 25 | 0.1–1000.0 | Straight-line pre-filter radius before route ranking runs. |
| `TravelBufferEnabled` | `true` | — | Kill switch for the travel-time-buffer eligibility gate between consecutive jobs. |
| `TravelHandoverBufferMinutes` | 15 | 0–240 | Minimum gap required between a provider's jobs, accounting for travel. |
| `MaxTravelRouteLookups` | 20 | 0–200 | Cost ceiling; past this, falls back to a sandbox estimate rather than blocking assignment. |

## 5. The tracking hub — routes, groups, auth

**Route**: `/hubs/tracking` (`HubRoutes.TrackingPath`), mapped by all three
APIs (consumer-api, provider-api, admin-api) under one shared hub type. This
is required, not tidiness: the SignalR Redis backplane fans a group message
out to every server subscribed under the hub's *type* name, and a
provider's location ping is ingested by provider-api but has to reach a
customer connection held by consumer-api. Three per-API hub classes would be
three disconnected group namespaces.

**Groups**: one group per booking, named `booking-tracking-{bookingId:D}`
(`TrackingGroups.Booking`).

**Auth model**: holding a valid JWT gets a socket connection, not a
booking. Authorization happens on `JoinBooking(Guid bookingId)`, not on
connect, via `BookingTrackingAuthorizer.CanTrackAsync`, which branches on
`RealtimeActorContext.Kind`:

| Actor | Allowed when |
|---|---|
| **Customer** | Owns the booking (`booking.CustomerId == customerId`) **and** the booking is currently trackable. |
| **Provider** | Is the provider on the booking's *live* assignment (`Assigned` or `Accepted` status) **and** the booking is currently trackable. |
| **Admin** | Holds the `bookings.read` permission (`AdminPermissionCatalog.BuildCode(AdminModules.Bookings, AdminPermissionAction.Read)`) — read, not write, since watching a booking's progress changes nothing. |
| **Unknown** | Always denied. |

A denied `JoinBooking` call throws one `HubException` with the same message
regardless of the reason ("no such booking" / "not yours" / "no longer
trackable") — the hub equivalent of the REST layer's 404-not-403 rule, so a
caller cannot use the response to learn whether a booking id exists.
`LeaveBooking` performs no authorization check at all: leaving a group you
were never in is a no-op, and refusing it would leak information
`JoinBooking` is careful not to.

The hub has no "send" RPC. Every payload it carries (status changes,
provider location, ETA) is produced by a REST call or a background
computation elsewhere, persisted there, and pushed from a domain-event
handler — a client that never opens a socket still gets the same data by
polling the relevant REST endpoint (`GET /bookings/{id}/tracking` on
consumer-api, `GET /admin/bookings/{id}/tracking` on admin-api).

## 6. Configuration surface — where the actual defaults live

None of `ProviderLocationIngest`, `BookingEta`, or `AutoAssignment` has a
section in any API's `appsettings*.json` today — all three run entirely on
the C# class defaults documented above in every environment. Only
`GoogleMaps` has an explicit section, and it is identical across all three
APIs' base `appsettings.json`:

```json
"GoogleMaps": {
  "Enabled": true,
  "TimeoutSeconds": 5,
  "CacheTtlSeconds": 60,
  "MaxDestinationsPerCall": 25,
  "MaxDestinationsPerEstimate": 100
}
```

No `ApiKey` appears in any appsettings file, by design — see §7.

## 7. The two Google Maps keys — server key vs. browser key

This feature uses **two separate Google Maps API keys**, restricted
differently, and they must never be the same key:

1. **Server-side key** (`GoogleMaps__ApiKey`) — read via standard ASP.NET
   Core configuration binding (`GoogleMapsOptions` binds section
   `"GoogleMaps"`; the double underscore is the env-var section separator).
   Used only for the Routes API `computeRouteMatrix` call from
   `GoogleMapsRouteEstimateProvider`. Comes from an environment
   variable/secret store — never a committed appsettings file. Restrict it
   in the Google Cloud Console to: the Routes API only, and by server IP
   (or equivalent network restriction for the deploy host).
2. **Browser-side key** (`NEXT_PUBLIC_GOOGLE_MAPS_API_KEY`) — read by
   `lib/googleMaps.ts` in both `frontend/customer-web` and
   `frontend/admin-web` (provider-web does not render a map). Loaded
   lazily — no `<script>` tag is injected until the tracking screen/card
   actually mounts, so this key is never on the critical path of a page
   that doesn't show a map. When absent, both apps degrade to a documented
   "no map" state rather than crashing — this is what keeps local
   development and CI runnable with no billing account at all; confirm
   this before assuming a missing key is a bug. Restrict this key in the
   Google Cloud Console by **HTTP referrer** to the deployed frontend
   origins — an IP restriction does not make sense for a key embedded in
   client-side JavaScript.

Neither `frontend/customer-web/.env.example` nor
`frontend/admin-web/.env.example` documents `NEXT_PUBLIC_GOOGLE_MAPS_API_KEY`
today — add it there (with a comment noting it is optional and what happens
when it is absent) the next time either file is touched.

See `docs/RUNBOOK-DEPLOYMENT.md` §142a.1 for the operational side: where
each key is provisioned, quota/billing alerting, and the restriction
settings above stated as a deploy checklist.

## 8. Sandbox-vs-Google summary (the one thing to remember)

If `GoogleMaps:ApiKey` is unset, or `GoogleMaps:Enabled` is `false`, **every**
route/ETA computation in this feature — auto-assignment ranking, ETA
recompute, everything — silently and correctly runs on the free local
sandbox estimator. This is the default state of local development and CI.
Nothing needs mocking, stubbing, or flagging to make that true; it is a
consequence of `RouteEstimateRegistration`'s startup binding, not a
special-cased test seam.

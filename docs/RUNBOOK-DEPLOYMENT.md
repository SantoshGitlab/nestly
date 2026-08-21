# RUNBOOK: Deploy, Rollback, Incident Response, On-Call (tasks 142a-142d)

Companion to `docs/DEVOPS.md` CI/CD PIPELINE and OBSERVABILITY sections, and
to `docs/RUNBOOK-BACKUP-RESTORE.md` (database-specific — that runbook is the
authority for backup/restore; this one covers everything else an on-call
engineer needs). Deployable units, environments, and the registry/host
OPEN DECISIONS are as documented in `docs/DEVOPS.md` — this runbook does not
duplicate them, only the operational steps that sit on top.

## 142a — Deploy

Three services deploy independently: `consumer-api`, `admin-api`,
`provider-api` (images built from `backend/*/Dockerfile`, pushed to GHCR per
`docs/DEVOPS.md` OPEN DECISIONS resolution in task 138a).

**Normal path — fully automatic, no manual steps:**

1. Merge a PR into `develop` → `.github/workflows/cd-staging.yml` runs the
   same build/test gate as `ci.yml`, builds and pushes the three images to
   `ghcr.io`, runs `database/scripts/apply-migrations.sh` against staging as
   its own job (always before deploy), then deploys via the `staging`
   GitHub Environment over SSH using `deploy/docker-compose.deploy.yml`.
2. Merge `develop` into `main` (or merge a PR targeting `main`) →
   `.github/workflows/cd-production.yml` runs the identical sequence
   against the `production` GitHub Environment. Production deploys pause
   for approval if **Settings → Environments → production → Required
   reviewers** is configured (documented in the workflow header as a
   repo-settings action — not expressible in YAML).

**Preconditions that make a deploy actually reach a host** (both workflows
fail fast and loudly, by design, until these exist — see `docs/DEVOPS.md`
OPEN DECISIONS 1-4): `STAGING_*` / `PRODUCTION_*` SSH host, user, and key
secrets, plus a provisioned Docker host running compose. There is no real
staging/production host today, so a deploy triggered now will pass build
and migration, then fail at the SSH step — check the Actions run log for
which secret is missing before assuming a code problem.

**Verifying a deploy landed:** hit the liveness (`/health/live`) and
readiness (`/health/ready`) endpoints each API exposes (wired via
`MapHealthChecks` in every `Program.cs` per `docs/DEVOPS.md` HEALTH CHECKS)
on all three services, then check `/metrics` (Prometheus scrape endpoint,
task 137) is serving the new build.

### 142a.1 — Google Maps API keys

Phase 16 (order tracking, `docs/TRACKING.md`) made routing and live tracking
depend on Google Maps — SRS §30.3 previously called maps "optional" for
address autocomplete/lat-long capture only; that is no longer the whole
picture. **Two separate keys** are required, provisioned and restricted
differently — see `docs/TRACKING.md` §7 for what each key does and why they
must not be the same key:

- **Server key** — set `GoogleMaps__ApiKey` as a secret on each API's
  deploy environment (same secret-store mechanism as the other
  `STAGING_*`/`PRODUCTION_*` secrets referenced above — never a committed
  appsettings value). In Google Cloud Console: restrict to the **Routes
  API** only, and restrict by server/IP.
- **Browser key** — set `NEXT_PUBLIC_GOOGLE_MAPS_API_KEY` as a build-time
  env var for `frontend/customer-web` and `frontend/admin-web` (not
  `provider-web`, which renders no map). In Google Cloud Console: restrict
  by **HTTP referrer** to the deployed frontend origin(s) — an IP
  restriction is meaningless for a key shipped in client-side JavaScript.

**Both keys are optional at the infrastructure level** — absent either one,
the corresponding feature degrades gracefully (server: routing falls back
to the free local sandbox estimator; browser: the tracking screen/card
renders without a map) rather than failing the deploy. Do not treat a
missing-key report as a deploy blocker by default; confirm first whether
degraded behavior is acceptable for that environment.

**Quota and billing alerting:** before enabling the server key in a
production-traffic environment, set a budget alert in Google Cloud Billing
for the Maps Platform project (Billing → Budgets & alerts), and set a quota
alert on the Routes API (APIs & Services → Quotas) at a threshold well
below the point where `GoogleMapsOptions.MaxDestinationsPerEstimate`'s cost
ceiling would be hit at expected traffic. No alert is wired into this
repo's own alerting today (`docs/DEVOPS.md` OPEN DECISIONS — monitoring
stack unresolved, same gap noted in 142c) — this is a Cloud Console
configuration step, not a code change, and it is separate from and in
addition to that unresolved decision.

## 142b — Rollback

`.github/workflows/rollback.yml`, triggered manually
(`workflow_dispatch`) — never automatic, so a bad deploy never
self-heals into a second bad deploy.

Inputs: `environment` (`staging`/`production`), `image_tag` (the prior known
good tag from the GHCR package list), optional `target_migration`.

1. **App rollback (always the first move):** redeploys `image_tag` via the
   same SSH + `docker-compose.deploy.yml` path as a normal deploy — no
   rebuild, so it's fast and doesn't re-run CI. `environment:
   ${{ inputs.environment }}` on this job means a production rollback still
   requires the same reviewer approval as a forward deploy — an incident is
   not a reason to bypass the approval gate.
2. **Database rollback (opt-in, only if `target_migration` is set):** runs
   `dotnet ef database update <target_migration>`. This is only safe when
   every EF Core migration between the current and target migration has a
   lossless `Down()` — verify that before using it. **Default guidance:
   redeploy the previous app image and forward-fix, rather than rolling the
   database back** — a fabricated automatic DB-rollback guarantee would be
   worse than an honest manual check here.

## 142c — Incident response

**Detection.** Three sources, in order of how fast they surface a problem:

1. `FailureRateAlertMonitor` (task 137) — a rolling per-category failure-rate
   monitor wired into payment (`PaymentWebhookService`), booking
   (`BookingService.CreateAsync`, `BookingMetricsHandler`), and notification
   (`NotificationDispatchService`) paths. When a category's failure rate
   crosses its configured threshold in `MetricsOptions`/`"Metrics"`, it
   raises an error-level structured log event with a distinct `EventId` and
   an `AlertCode` property (`Payment.FailureRateAlert`,
   `Booking.FailureRateAlert`, `Booking.SlotCapacityReached`,
   `Notification.FailureRateAlert` per channel) — see
   `backend/shared/Infrastructure/Observability/MetricsAlertEvents.cs`.
   No external paging destination is wired yet (`docs/DEVOPS.md` OPEN
   DECISIONS — monitoring/alerting stack unresolved): today this means
   grepping/alerting on these `AlertCode`s in whatever log aggregator reads
   the structured logs, until a Slack/PagerDuty/email sink is chosen.
2. `/metrics` Prometheus scrape endpoint on each API — request rate,
   latency, error rate (counters/histograms on the `"Nestly"` Meter).
3. Health check failures (`/health/live`, `/health/ready`) surfaced by
   whatever orchestrator/load balancer is in front of the APIs once one
   exists.

**Response steps, in order:**

1. **Triage severity.** Payment and booking failures are availability-priority
   per `docs/DEVOPS.md` SCALABILITY AND AVAILABILITY (SRS §29.4) — treat
   `Payment.FailureRateAlert` and `Booking.FailureRateAlert` as the highest
   urgency; a single-channel `Notification.FailureRateAlert` (e.g. SMS
   provider down) is lower urgency since booking/payment still succeed.
2. **Confirm scope.** Check whether the failure is isolated to one API
   (consumer/admin/provider) or systemic (e.g. shared Postgres/Redis down) —
   the three APIs deploy and scale independently, so a bad `consumer-api`
   deploy does not imply `admin-api`/`provider-api` are affected.
3. **Mitigate.** If the alert correlates with a recent deploy, roll back
   (142b) first — restoring service takes priority over root-causing during
   an active incident. If it does not correlate with a deploy (e.g.
   downstream payment gateway outage), there is no code-side rollback to
   perform; monitor the failure-rate metric for recovery and communicate
   status.
4. **Idempotency is the safety net, not a substitute for care.** Payment
   webhook processing and booking creation are both idempotent by design
   (`docs/DEVOPS.md` GRACEFUL SHUTDOWN; task 69's signed-callback + dedup
   handling; task 135b's `TryAddAsync`/unique-index dedup fallback) — retried
   in-flight work during a redeploy or restart will not double-charge or
   double-book, but this does not mean skipping the triage/mitigate steps.
5. **Verify recovery.** Confirm the triggering `AlertCode`'s failure rate has
   dropped back under threshold and the relevant health checks are green
   before closing the incident.
6. **Write it up.** Record what happened, what the log evidence was
   (`AlertCode` + timestamps), what mitigated it, and any follow-up task —
   add follow-up work to `tasks.csv` under the relevant phase rather than a
   separate incident tracker, since that CSV is this repo's single backlog.

## 142d — On-call basics

- **Scope of on-call today:** this repo has CI/CD, metrics, and structured
  alert logging (Phase 8), but no real staging/production host and no
  external paging integration yet (`docs/DEVOPS.md` OPEN DECISIONS). On-call
  currently means: someone is watching the structured logs / `/metrics`
  endpoints and knows this runbook — not "someone gets paged at 3am,"
  since there is nowhere for a page to come from yet. Update this section
  once a paging destination is decided.
- **What to have open:** `/metrics` for all three APIs, log aggregator
  filtered to `AlertCode` on the `Nestly` structured-log source, and this
  runbook plus `docs/RUNBOOK-BACKUP-RESTORE.md`.
- **Escalation path for a database-affecting incident:** stop, do not
  improvise a manual `psql` fix under pressure — use
  `docs/RUNBOOK-BACKUP-RESTORE.md`'s tested restore procedure
  (`database/scripts/backup-postgres.sh` /
  `database/scripts/restore-postgres.sh`), which has been drilled end-to-end
  with row-count and checksum verification.
- **Permissions an on-call engineer needs:** access to trigger
  `rollback.yml` (`workflow_dispatch` on this repo), the `staging`/
  `production` GitHub Environments (for approval gates), and read access to
  wherever structured logs land.
- **What on-call is not:** a substitute for fixing the root cause during
  business hours — mitigation (rollback, forward-fix) restores service;
  the incident write-up (142c step 6) is what turns into a real fix.

## 389 — First-boot: making a new database bookable

A database built from migrations is **unbookable and silent about it**. Slot
and serviceability lookups fail closed by design, so every API returns a
correct, empty answer and nothing looks broken —
[QA-REPORT-2026-08-18.md](QA-REPORT-2026-08-18.md) Phase 1 found exactly this
state in the seeded dev database, where no customer could book any service.

Run this immediately after the first deploy to any new environment, before
announcing it to anyone.

### Step 1 — Ask the platform

    curl -s https://<host>/health/bootstrap

`Healthy` means a customer can find and book something. `Degraded` names every
broken link in `gaps`, each with the remedy. The same verdict is written to the
log once per process start — a `Warning` from
`Nestly.Infrastructure.Persistence.Readiness.BookabilityProbe` beginning
`NOTHING CAN BE BOOKED`.

The endpoint is deliberately **not** part of `/health/ready`. An unseeded
database is the correct state of a freshly migrated deployment, and failing
readiness would pull the host out of rotation before an operator could reach
the admin API to seed it — the check would prevent its own remedy.

### Step 2 — Create the catalog

Categories, services and pricing are product data and are entered through
admin-web. Nothing in `database/seed/` creates them: putting unreviewed
commercial content into a production database by script is not a thing this
runbook will ask you to do.

### Step 3 — Close the geography, serviceability and slot chain

    psql "$DATABASE_URL" \
      -v state_name="Karnataka" -v state_code="KA" \
      -v city_name="Bengaluru" \
      -v pincodes="560001,560002,560034" \
      -v capacity=5 \
      -f database/seed/bootstrap-bookability.sql

Once per city you serve. Idempotent — re-run it after adding services and it
will map the new ones and change nothing else.

It creates the state, city, a default zone, the pincodes and one locality per
pincode; maps every active service into those pincodes; lists their categories
in the city; and creates three standard slot windows with rules for all seven
days. Narrowing coverage, subdividing zones and removing unserved days are
admin-UI decisions afterwards.

### Step 4 — Confirm

Re-run step 1 and expect `Healthy`. Then book something through customer-web
end to end. Row counts are not proof: the endpoint walks the real chain, which
is why step 1 and not a `SELECT count(*)` is the check that closes this out.

### Gap codes

| Code | What it means |
|---|---|
| `bookability.no_active_city` | Geography never seeded — run step 3 |
| `bookability.no_active_pincode` | City exists with no pincodes — run step 3 |
| `bookability.no_locality` | Pincodes with no locality. Addresses join geography through locality and the slot API is entered by locality id, so both break |
| `bookability.no_active_service` | Catalog is empty — step 2, not step 3 |
| `bookability.no_service_pincode_mapping` | Catalog and geography exist but are not joined — the QA sweep's finding |
| `bookability.no_slot_window` | No booking windows for the city |
| `bookability.no_slot_window_rule` | Windows exist but no day-of-week rules, so they are never offered. Subtle: the admin UI shows the window as present |
| `bookability.chain_disjoint` | Everything exists but not in one city — check `slot_window.city_id` against `pincode.city_id` |
| `bookability.no_category_city_mapping` | Bookable by API, invisible in the app: the category is not listed in that city |

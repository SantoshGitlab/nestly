# LAUNCH-COST.md

Production launch plan and infrastructure cost sizing for the Jaipur launch.

## STATUS

**Recommendation, not policy.** This document proposes a production rollout
sequence and a cost model. It does not commit to a cloud provider, a vendor,
or a budget — those are decisions for whoever owns infra/ops spend. It
resolves none of DEVOPS.md's six **OPEN DECISIONS** (hosting platform,
registry, orchestrator, secret store, monitoring stack, CDN/media provider);
it only recommends a specific, low-cost answer for each, sized to a
single-city MVP launch rather than steady-state scale.

Companion to [DEVOPS.md](DEVOPS.md) (containerization/CI/CD standards, which
this plan follows) and [PRICING.md](PRICING.md) (customer-facing pricing —
unrelated to the infrastructure cost discussed here).

## 1. PRODUCTION ROLLOUT SEQUENCE

Assumes code is feature-complete and the local QA sweep has passed (see
[QA-REPORT-2026-08-18.md](QA-REPORT-2026-08-18.md) and the booking-lifecycle
sweep referenced in git history around 2026-08-20).

### 1.1 Close the open infra decisions

For a single-city MVP, avoid over-provisioning:

| Decision | Recommendation | Why |
|---|---|---|
| Hosting platform | Managed containers with scale-to-zero (Azure Container Apps, or AWS Fargate) — not Kubernetes | .NET-native path, no idle-capacity cost overnight |
| Container registry | Whatever ships with the hosting choice (ACR/ECR) | avoids a second vendor relationship |
| Database | Managed serverless Postgres (Neon or Supabase) over a fixed always-on instance | scales to zero between traffic, still standard Npgsql/EF Core compatible |
| Redis | Serverless pay-per-request (Upstash) over a provisioned instance | near-zero cost at launch traffic |
| Secret store | Azure Key Vault / AWS Secrets Manager, injected as env vars at deploy time | matches DEVOPS.md's "secrets never in source" rule |
| Monitoring | Free tier (Application Insights or Grafana Cloud) until incident volume justifies a paid plan | structured logs + correlation IDs already required by SRS §29.6 |
| CDN / media | Cloudflare (free CDN + TLS) + Cloudflare R2 or Backblaze B2 for photo storage | no egress fees, matters once completion-proof/review photos accumulate |

### 1.2 Real integrations currently stubbed for local dev

- **SMS/email OTP vendor** — `SandboxNotificationProvider` (see
  `backend/shared/Infrastructure/Services/SandboxNotificationProvider.cs`)
  never calls a real vendor and never logs the OTP, by design. Production
  needs a real `INotificationProvider` implementation (MSG91 for SMS —
  cheaper than Twilio in India — SendGrid/SES for email) registered per
  environment via `AddInfrastructure`. No other change to the OTP flow is
  required; the interface is already the seam.
- **Payment gateway** — confirm live-mode merchant credentials, not sandbox.
- **Maps API key** — required for the live tracking map; missing key
  currently degrades to "Live map unavailable" without breaking tracking
  logic itself.

### 1.3 CI/CD (already specified in DEVOPS.md — restated as a checklist)

- `develop` → auto-deploy Staging; `main` → deploy Production behind a
  **manual approval** gate.
- EF Core migrations run as an explicit, ordered deployment step — never
  implicitly at app startup in Production.
- One Dockerfile per deployable unit (6 total: consumer-api, admin-api,
  provider-api, customer-web, admin-web, provider-web — provider-api and
  provider-web are missing from DEVOPS.md's deployable-units table and
  should be added there).
- Multi-stage builds, non-root containers, pinned base images.

### 1.4 Pre-launch checklist

- [ ] `NEXT_PUBLIC_ENABLE_DEV_AUTH` and `DevAuth:Key` absent from Production
      config (structurally impossible to enable as-is per DEVOPS.md's
      dev-only-provider-test-login section — just don't add a `DevAuth`
      section to `appsettings.Production.json`)
- [ ] Liveness/readiness health checks wired into the orchestrator's
      routing/restart logic
- [ ] Automated daily DB backups with a **tested** restore (see
      RUNBOOK-BACKUP-RESTORE.md) — not just "enabled"
- [ ] Alerting live specifically for payment, booking, and notification
      failures (SRS §29.6 critical-path requirement)
- [ ] Rate limits and lockout thresholds sane for real (non-seed) traffic
- [ ] TLS/custom domains on all three frontends; CORS allowed-origins
      updated from localhost ports to production domains
- [ ] Full booking lifecycle run once against Staging with real (non-sandbox)
      integrations before promoting to `main`

### 1.5 Day-one operations

- Blue-green or canary if the platform supports it; otherwise manual-approval
  deploy with fast rollback to the previous image tag (already a stated CD
  requirement).
- Watch payment/OTP/notification alerts closely for the first 48 hours —
  these are the SRS-flagged availability-critical paths.
- Don't spend pre-launch time tuning autoscaling for load you haven't
  measured yet; a single-city launch has bounded traffic — revisit once real
  usage data exists.

## 2. BOOKING VOLUME SIZING

No real traffic data exists yet. This ramp assumes a new single-city
entrant, not incumbent-scale volume — Urban Company's numbers don't apply to
a launch month.

| Phase | Timeline | Bookings/mo | Registered users | Basis |
|---|---|---|---|---|
| Soft launch | Month 1 | 50–200 | 200–500 | word-of-mouth, one category, manual QA of every order |
| Early traction | Month 2–3 | 300–800 | 1,000–2,500 | first marketing push, recurring/AMC plans start compounding |
| Post-PMF | Month 4–6 | 1,000–3,000 | 3,000–8,000 | if recurring plans land — see note below |

**Recurring/AMC skews bookings faster than user growth.** A single AMC
customer can generate 4–12 bookings/year with zero incremental acquisition
cost, so infra should be sized for "bookings scale faster than signups" once
[AMC.md](AMC.md)'s recurring plans get traction — this is the one feature
that can spike booking volume without a matching spike in traffic/compute
per user.

## 3. COST BY PHASE

Estimates assume the serverless/scale-to-zero choices from §1.1, not a
fixed always-on baseline.

| Item | Month 1 (50–200 bookings) | Month 3 (300–800) | Month 6 (1–3k) |
|---|---|---|---|
| Compute (3 APIs + 3 web) | $20–40 | $60–100 | $120–200 |
| Postgres | $15–25 (shared/burstable) | $40–60 | $80–120 |
| Redis | $0–10 (free tier) | $15 | $30 |
| SMS OTP (~3/user incl. resend) | $3–8 | $15–30 | $50–100 |
| Maps / CDN / storage | $0 (free tiers) | $0–10 | $15–30 |
| **Total/mo** | **~$40–90** | **~$130–215** | **~$300–480** |

Month 1 is the number that matters right now: **under $100/mo is
achievable**, well below the ~$200–550/mo steady-state figure a
naively-provisioned (always-on, fixed-instance) setup would run.

Payment-gateway fees (~2% + ₹2–3/txn) are **not** an infra cost — they come
off revenue per transaction, not a monthly bill.

## 4. COST MINIMIZATION

**Compute — biggest lever**
- Don't run 6 always-on services at launch. The 3 APIs are stateless and
  share one DB — put them behind one small container group/reverse proxy
  instead of 3 separate App Service plans. Deploy the 3 Next.js apps on
  Vercel's free tier instead of paying for container hosting.
- Use scale-to-zero platforms (Azure Container Apps, Fargate with
  min-instance=0) — a single-city app has near-zero traffic 12am–6am and
  shouldn't be billed for it.

**Database — second biggest lever**
- Neon or Supabase (serverless Postgres, scales to zero, free/low tier)
  instead of a fixed managed instance billed 24/7. Standard EF Core/Npgsql
  connections work unchanged.
- Upstash (serverless Redis, pay-per-request) instead of a provisioned
  instance.

**SMS/OTP — scales with growth, optimize per-unit cost early**
- MSG91 over Twilio: typically 3–5x cheaper per SMS in India.
- Consider WhatsApp Business API for OTP where the user has WhatsApp —
  often cheaper per-message and higher deliverability in India than SMS.

**CDN / storage / maps**
- Cloudflare free tier (CDN, TLS, DDoS) over a paid cloud CDN.
- Cloudflare R2 or Backblaze B2 over S3/Azure Blob — no egress fees, matters
  once completion-proof and review photos accumulate.
- Google Maps' $200/mo free credit almost certainly covers single-city
  tracking-page load volume at launch scale — don't pre-pay for this.

**Monitoring**
- Free tiers (Grafana Cloud free, Application Insights' free monthly
  ingestion quota) are enough until real incident volume justifies a paid
  plan.

**What NOT to cut**
- Backups — non-negotiable even at near-zero traffic; this is the "cheap
  now, catastrophic later" trap.
- Payment/booking/notification alerting — SRS §29.6 names these the
  availability priority. Skipping this to save a few dollars a month is the
  wrong trade.

Net effect of the above: Month 1 realistically lands around **$30–60/mo**,
and Month 6 at 1–3k bookings stays closer to **$200–300/mo** instead of
$480, by deferring anything provisioned/always-on until usage actually
demands it.

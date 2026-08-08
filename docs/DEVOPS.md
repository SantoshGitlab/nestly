# DEVOPS.md

Docker, CI/CD, deployment, monitoring and operations standards.

## PURPOSE

This document defines the DevOps standards for building, packaging, deploying, and operating the Nestly platform.

It is the single source of truth for containerization, pipelines, environment management, health checks, and observability.

Open platform decisions (cloud provider, CI platform, registry, orchestrator) are tracked in OPEN DECISIONS below and must be resolved before production deployment.

## DEPLOYABLE UNITS

The platform produces four deployable applications plus supporting services:

| Unit | Source | Runtime |
|---|---|---|
| Consumer API | backend/consumer-api | ASP.NET Core (.NET 8) |
| Admin API | backend/admin-api | ASP.NET Core (.NET 8) |
| Customer Web | frontend/customer-web | Next.js (Node.js) |
| Admin Web | frontend/admin-web | Next.js (Node.js) |
| PostgreSQL | managed / container | Database |
| Redis | managed / container | Cache |
| Hangfire | hosted inside API process | Background jobs |

## CONTAINERIZATION

Every deployable unit must have its own Dockerfile.

Rules:

- Use multi-stage builds (SDK image for build, runtime image for execution).
- Use official Microsoft and Node base images with pinned versions.
- Run containers as a non-root user.
- Keep images minimal; no build tools in runtime images.
- Configuration comes from environment variables, never baked into images.
- Provide a docker-compose file for local development (APIs + PostgreSQL + Redis).

## ENVIRONMENTS

Four environments are supported, matching the configuration standards in DOTNET.md:

- Development
- Testing
- Staging
- Production

Rules:

- Same artifact/image is promoted across environments; only configuration differs.
- Secrets are supplied per environment, outside source control.
- Staging must mirror production topology as closely as possible.
- Production configuration changes must be auditable.

## CONFIGURATION AND SECRETS

- All configuration is external (environment variables or mounted config).
- Strongly typed configuration on the application side (see DOTNET.md).
- Secrets (connection strings, JWT keys, OTP hashing pepper, payment gateway keys, SMS/email provider keys) must come from a secret store — never from source code or images.
- Local development may use dotnet user-secrets / .env files that are gitignored.

## CI/CD PIPELINE

Every push to a feature branch and every pull request must run CI.

Required CI stages:

1. Restore and build (backend solution and frontend apps)
2. Unit tests
3. Integration tests (with disposable PostgreSQL/Redis containers)
4. Static analysis / linting (dotnet analyzers, ESLint, TypeScript checks)
5. Docker image build
6. Security/dependency scan

Required CD stages:

1. Publish versioned images to the container registry
2. Deploy to Staging automatically from develop
3. Deploy to Production from main with manual approval
4. Run EF Core migrations as an explicit, ordered deployment step
5. Support rollback to the previous image version

Branch strategy: feature branches → develop (staging) → main (production).

## DATABASE OPERATIONS

- Schema changes only through EF Core migrations (see DATABASE.md).
- Migrations run as a separate deployment step, not implicitly at app startup in Production.
- Backups: automated daily backups with tested restore procedure.
- Seed data scripts live in database/seed and must be idempotent.

## HEALTH CHECKS

Every API must expose:

- Liveness endpoint (process is up)
- Readiness endpoint (database, Redis, and critical dependencies reachable)

Orchestrators and load balancers must use these endpoints for routing and restarts.

## GRACEFUL SHUTDOWN

- Applications must handle SIGTERM and finish in-flight requests before exiting.
- Hangfire background jobs must support cancellation and safe re-execution.
- Payment and booking webhook processing must be idempotent so interrupted work can retry safely (see SRS §11.11, §26.3).

## OBSERVABILITY

Per SRS §29.6, the platform requires:

- Structured application logs (see CLAUDE.md LOGGING rules — never log secrets, tokens, or PII)
- Error logs with diagnostic context
- Audit logs for critical business and admin actions
- Metrics: request rate, latency, error rate, DB pool usage, background job health
- Health checks wired into monitoring
- Alerting for critical failures — payment, booking, and notification failures at minimum

Correlation IDs must flow from the frontend through APIs into logs.

## SCALABILITY AND AVAILABILITY

- APIs must be stateless so they can scale horizontally (session state in Redis/JWT).
- Customer booking flows are the availability priority (SRS §29.4).
- Static frontend assets should be served via CDN where possible.
- Traffic spikes during promotions must be considered in capacity planning (SRS §6.2).

## DEV-ONLY PROVIDER TEST LOGIN

QA/browser-automation tools that exercise provider-web's authenticated
screens (jobs, availability, earnings, profile) cannot complete a real OTP
login: some automation tools refuse to type OTP codes at all, treating it as
an auth-bypass action, and there is no way to read a generated OTP out of
`SandboxNotificationProvider` through the UI. To unblock that testing without
touching real OTP verification, provider-api exposes one dev-only endpoint
that mints a real session for the seeded `+919888888888` E2E Test Provider
(`database/seed/dev-provider-seed.sql`), skipping OTP entirely.

**NEVER enable this in Staging or Production.** It is gated so that doing so
is a structural impossibility, not a matter of convention:

1. **Route only exists in Development.** `POST /api/v1/auth/dev/login-as-provider`
   is registered inside `if (app.Environment.IsDevelopment())` in
   `backend/provider-api/ProviderApi/Program.cs` — in any other environment
   the route is never mapped, so it 404s. This is on top of, not instead of,
   the usual environment check.
2. **Second gate: a shared secret.** The caller must send the request header
   `X-Dev-Auth-Key` matching `DevAuth:Key` from configuration. That key is
   defined **only** in `appsettings.Development.json` — it does not exist in
   `appsettings.json` or `appsettings.Production.json`, so even a
   misconfigured deployment has nothing to match the header against.
3. **Additive, not a bypass branch.** The endpoint calls a new
   `ProviderLoginService.DevLoginAsync` method that reuses the same
   session-issuing code (`IssueSessionAsync`, token generation) as a real
   login. It does not modify `AuthController`'s `login/otp/verify` endpoint
   or `LoginWithOtpAsync` in any way.
4. **Loud, auditable logging.** Every call logs a structured warning
   (`"SECURITY: dev-only auth bypass used"`) so any accidental exposure
   would show up immediately in logs/alerts.

### Enabling it locally

Backend (`backend/provider-api/ProviderApi`), already set in
`appsettings.Development.json`:

```json
"DevAuth": { "Key": "dev-only-provider-auth-key-local-1234567890" }
```

Frontend (`frontend/provider-web`), in a gitignored `.env.local` (never
commit these):

```
NEXT_PUBLIC_ENABLE_DEV_AUTH=true
DEV_AUTH_KEY=dev-only-provider-auth-key-local-1234567890
```

`NEXT_PUBLIC_ENABLE_DEV_AUTH` controls whether the "Dev sign in (test
provider)" button renders on `/login` — it is unset by default, so the
button does not exist in a normal checkout. `DEV_AUTH_KEY` is deliberately
**not** `NEXT_PUBLIC_*`: it is read server-side only, inside the Next.js
route handler at `frontend/provider-web/src/app/api/dev-login/route.ts`,
which proxies to provider-api with the `X-Dev-Auth-Key` header attached. The
key never reaches the browser bundle.

With both set, clicking the button on `/login` signs in as the seeded E2E
Test Provider and lands on `/jobs` with a normal, fully-working session
(same access/refresh tokens a real OTP login would produce).

## OPEN DECISIONS

Decided:

- CI platform: **GitHub Actions** (`.github/workflows/ci.yml` — backend build/test with disposable Postgres/Redis, frontend lint/build, Docker image builds)

To be finalized before production setup:

1. Cloud provider / hosting platform
2. Container registry
3. Orchestrator (managed containers vs Kubernetes)
4. Secret store implementation
5. Monitoring/alerting stack
6. CDN / media storage provider

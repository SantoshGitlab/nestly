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
- Secrets (connection strings, JWT keys, payment gateway keys, SMS/email provider keys) must come from a secret store — never from source code or images.
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

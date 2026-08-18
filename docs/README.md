# Nestly Documentation Index

Master index for the project documentation suite.

## PURPOSE

This document is the entry point for the project's documentation.

It defines:

- What each document is responsible for.
- Which document owns each topic.
- Where to find specific information.
- How to avoid duplicated documentation.

Every topic has exactly **one authoritative document**.

## DOCUMENTS

| Document | Responsibility |
|---|---|
| [ORIENTATION.md](ORIENTATION.md) | **Start here.** What exists today vs. what is planned, how the layers fit together, and the non-obvious rules. The only document describing current repository state |
| [../.claude/CLAUDE.md](../.claude/CLAUDE.md) | AI behavior, workflow, reasoning and response rules |
| [PROJECT.md](PROJECT.md) | Business domain, project vision, goals, users and modules |
| [MARKET.md](MARKET.md) | Market context, competitive landscape, revenue-model thesis, launch strategy and the commercial gap register for the Jaipur launch market |
| [LAUNCH-READINESS-AUDIT.md](LAUNCH-READINESS-AUDIT.md) | Evidence-based audit (2026-08-17) of what is actually implemented versus what ORIENTATION.md, the specs and `tasks.csv` claim |
| [SRS.md](SRS.md) | Full Software Requirements Specification (v2) — functional, workflow, validation, RBAC, screen, API, and operational requirements |
| [WORKFLOW.md](WORKFLOW.md) | Visual (Mermaid) workflow diagrams for project understanding — not authoritative, defers to SRS.md on conflict |
| [UI-GUIDE.md](UI-GUIDE.md) | Screenshot-illustrated walkthrough of each app's main screens, plus first-time local setup/seed/credentials instructions — companion to WORKFLOW.md, not authoritative |
| [ARCHITECTURE.md](ARCHITECTURE.md) | System architecture, layers, module boundaries and dependencies |
| [CODING-STANDARDS.md](CODING-STANDARDS.md) | Naming, code style, readability and general coding conventions |
| [DOTNET.md](DOTNET.md) | .NET 8, ASP.NET Core and framework-specific development standards |
| [DATABASE.md](DATABASE.md) | PostgreSQL, EF Core, schema design, indexing and data access standards |
| [API.md](API.md) | REST API conventions, DTOs, versioning and endpoint design |
| [FRONTEND.md](FRONTEND.md) | Next.js, React, TypeScript and frontend architecture |
| [SECURITY.md](SECURITY.md) | Authentication, authorization, secrets and security practices |
| [TESTING.md](TESTING.md) | Unit, integration, API and end-to-end testing strategy |
| [DEVOPS.md](DEVOPS.md) | Docker, CI/CD, deployment, monitoring and operations |
| [RUNBOOK-BACKUP-RESTORE.md](RUNBOOK-BACKUP-RESTORE.md) | Tested PostgreSQL backup/restore procedure (companion to DEVOPS.md's backup requirement) |
| [RUNBOOK-DEPLOYMENT.md](RUNBOOK-DEPLOYMENT.md) | Deployment procedure (companion to DEVOPS.md) |
| [QA-REPORT-2026-08-07.md](QA-REPORT-2026-08-07.md) | Release-readiness QA report — feature inventory and the current **NO-GO** verdict. Source of the one open backlog row (task 318) |
| [ENHANCEMENT-BACKLOG-2026-08-08.md](ENHANCEMENT-BACKLOG-2026-08-08.md) | Verified spec-vs-code gaps with file:line evidence. Per ORIENTATION.md, this is where the next task comes from |
| [UAT-REPORT.md](UAT-REPORT.md) | User acceptance testing results |
| [BOOKING-FLOW-AUDIT.md](BOOKING-FLOW-AUDIT.md) | Point-in-time audit of the booking funnel (source of the Phase 13 defect rows) |
| [CATALOG-ARCHITECTURE-REVIEW.md](CATALOG-ARCHITECTURE-REVIEW.md) | Point-in-time review of the catalog hierarchy (service groups, variants, add-on groups) |
| [migrations-audit.md](migrations-audit.md) · [migrations-plan.md](migrations-plan.md) | Point-in-time migration audit and remediation plan |
| [PHASE-12-HANDOFF.md](PHASE-12-HANDOFF.md) · [PHASE-16-CLOUD-BRIEF.md](PHASE-16-CLOUD-BRIEF.md) | Historical phase handoff notes — superseded, kept for provenance |
| [PROVIDER.md](PROVIDER.md) | Provider / Vendor module specification (Phase 7) |
| [REFERRAL.md](REFERRAL.md) | Referral (Refer & Earn) module specification (Phase 9) |
| [PRODUCT-ENHANCEMENTS.md](PRODUCT-ENHANCEMENTS.md) | Subscription, Recurring Bookings, In-App Chat, Completion Verification specification (Phase 10) |
| [NESTLY-COINS.md](NESTLY-COINS.md) | Nestly Coins (reorder loyalty currency for customers and providers) specification (Phase 11) |
| [AMC.md](AMC.md) | Annual Maintenance Contract module specification: prepaid entitlement drawdown, redemption, renewal pipeline (Phase 20) |
| [TRACKING.md](TRACKING.md) | End-to-end order tracking (Phase 16) — state machine, location ingest, ETA pipeline, tracking hub, and the Google Maps configuration surface |
| [tasks.csv](tasks.csv) | Development backlog — phased tasks, priorities and dependencies |
| [archive/](archive/) | Original Word-format versions of these documents (historical) |

## TOPIC OWNERSHIP

| Topic | Owner Document |
|---|---|
| AI behavior | CLAUDE.md |
| Business vision | PROJECT.md |
| Business terminology | PROJECT.md |
| Market and competitor analysis | MARKET.md |
| Implementation status ("what is built") | ORIENTATION.md |
| Status-claim verification and audit history | LAUNCH-READINESS-AUDIT.md |
| Release readiness / go-no-go | QA-REPORT-2026-08-07.md |
| Spec-vs-code gap backlog | ENHANCEMENT-BACKLOG-2026-08-08.md |
| Launch market strategy | MARKET.md |
| Revenue models and margin thesis | MARKET.md |
| Pricing posture and go-to-market | MARKET.md |
| Functional requirements | SRS.md |
| Booking lifecycle and workflows | SRS.md |
| RBAC requirements | SRS.md |
| System architecture | ARCHITECTURE.md |
| Module boundaries | ARCHITECTURE.md |
| Dependency rules | ARCHITECTURE.md |
| Layer responsibilities | ARCHITECTURE.md |
| Naming conventions | CODING-STANDARDS.md |
| Code organization | CODING-STANDARDS.md |
| Code readability | CODING-STANDARDS.md |
| .NET conventions | DOTNET.md |
| ASP.NET Core | DOTNET.md |
| Dependency Injection | DOTNET.md |
| Middleware | DOTNET.md |
| Configuration | DOTNET.md |
| EF Core usage | DATABASE.md |
| PostgreSQL | DATABASE.md |
| Schema design | DATABASE.md |
| Migrations | DATABASE.md |
| Transactions | DATABASE.md |
| Indexes | DATABASE.md |
| Query optimization | DATABASE.md |
| REST standards | API.md |
| API versioning | API.md |
| DTOs | API.md |
| Status codes | API.md |
| Request/Response contracts | API.md |
| React | FRONTEND.md |
| Next.js | FRONTEND.md |
| TypeScript | FRONTEND.md |
| Components | FRONTEND.md |
| Authentication | SECURITY.md |
| Authorization | SECURITY.md |
| Secrets management | SECURITY.md |
| Data protection | SECURITY.md |
| Unit Testing | TESTING.md |
| Integration Testing | TESTING.md |
| API Testing | TESTING.md |
| Docker | DEVOPS.md |
| CI/CD | DEVOPS.md |
| Deployment | DEVOPS.md |
| Monitoring | DEVOPS.md |
| Provider module design | PROVIDER.md |
| Live order tracking | TRACKING.md |
| Location ingest throttling/retention | TRACKING.md |
| ETA / routing pipeline | TRACKING.md |
| Google Maps API key management | TRACKING.md |
| AMC / entitlement contracts | AMC.md |
| Development backlog | tasks.csv |

## OWNERSHIP RULES

Every topic belongs to one document.

Do not duplicate guidance across multiple documents.

If a topic needs additional context, reference the owning document instead of repeating the content.

### Implementation status has exactly one owner

**[ORIENTATION.md](ORIENTATION.md) owns "what is built".** No other document
may assert implementation status.

Module specifications describe *what the module should be*, not whether it
exists. A spec's `STATUS` section may state which phase delivered it and link
to ORIENTATION.md — it must not carry a standing "not implemented" claim,
because nothing keeps such a claim in sync with the code.

This rule exists because it was broken. On 2026-08-17, four specifications
(`PRODUCT-ENHANCEMENTS.md`, `REFERRAL.md`, `NESTLY-COINS.md`, `PROVIDER.md`)
still read *"Not implemented"* for modules that had shipped phases earlier —
which in turn caused a competitive analysis to be written against a product
position that had not been true for weeks. See
[LAUNCH-READINESS-AUDIT.md](LAUNCH-READINESS-AUDIT.md).

## WRITING PRINCIPLES

Every document should be:

- Focused
- Concise
- Actionable
- Easy to maintain
- Easy for AI to understand
- Free from unnecessary repetition

One document = One responsibility.

## CHANGE POLICY

Before adding new documentation:

1. Identify the topic.
2. Find the owning document.
3. Update only that document.
4. Remove duplicate guidance if it exists elsewhere.
5. Keep the documentation suite consistent.

## SUCCESS CRITERIA

A high-quality documentation suite should have:

- Clear ownership
- No duplicated topics
- No conflicting guidance
- Consistent terminology
- Simple navigation
- Easy maintenance
- AI-friendly structure

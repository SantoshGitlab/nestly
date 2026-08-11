# ARCHITECTURE.md

Enterprise System Architecture Blueprint

## PURPOSE

This document defines the architectural blueprint of the Nestly platform.

It describes how the system is organized, how major components interact, how requests are processed, and the architectural principles that must be followed during development.

This document is the single source of truth for all architecture-related decisions.

## ARCHITECTURAL OBJECTIVES

The architecture is designed to achieve:

- Scalability
- Maintainability
- Reliability
- Security
- Extensibility
- Testability
- Observability
- Performance
- High Availability

## ARCHITECTURE STYLE

Nestly follows a **Modular Monolith** architecture built on:

- Clean Architecture
- Domain-Driven Design (DDD) principles
- Layered Architecture
- REST-based communication
- Event-driven processing where appropriate

Business modules are independent and designed to support future migration to Microservices with minimal changes.

## HIGH-LEVEL SYSTEM ARCHITECTURE

```
                       Users
                         │
                         ▼
              Next.js Web Application
                         │
                         ▼
              ASP.NET Core REST APIs
                         │
                         ▼
              ┌─────────────────────┐
              │  Application Layer  │
              └─────────────────────┘
                         │
                         ▼
              ┌─────────────────────┐
              │    Domain Layer     │
              └─────────────────────┘
                         │
                         ▼
              ┌─────────────────────┐
              │ Infrastructure Layer│
              └─────────────────────┘
                │        │        │
                ▼        ▼        ▼
           PostgreSQL  Redis  External Services
                          │
                          ▼
                       Hangfire
```

## REQUEST PROCESSING FLOW

Every request follows the same architectural pipeline.

```
Client
  ↓
Presentation Layer
  ↓
Application Layer
  ↓
Domain Layer
  ↓
Infrastructure Layer
  ↓
Database / External Services
  ↓
Response
```

#### Processing Rules

- Presentation handles HTTP communication.
- Application coordinates business use cases.
- Domain executes business rules.
- Infrastructure manages technical concerns.
- Persistence stores and retrieves data.
- Responses return through the same pipeline.

Business logic must remain inside the Domain layer.

## MODULE ORGANIZATION

The application is divided into independent business modules.

Examples include:

- Identity
- Customer
- Provider
- Category
- Service
- Booking
- Payment
- Notification
- Review
- Administration
- Reporting

Each module owns:

- Business logic
- Application services
- Domain model
- Persistence
- Internal implementation

Modules expose only the interfaces required by other modules.

## MODULE INTERACTION DIAGRAM

Identity │ ┌──────────────┼──────────────┐ ▼ ▼ Customer Provider │ │ └──────────────┬───────────────┘ ▼ Booking │ ┌────────────┼────────────┐ ▼ ▼ Payment Notification │ ▼ Reporting

#### Interaction Principles

- Modules communicate through well-defined interfaces.
- No direct database access between modules.
- Avoid circular dependencies.
- Minimize coupling.
- Preserve module independence.

## LAYER RESPONSIBILITIES

### Presentation Layer

Responsible for:

- HTTP communication
- Request routing
- Input validation
- Response generation

### Application Layer

Responsible for:

- Use case orchestration
- Workflow coordination
- Transaction boundaries
- Calling domain services

### Domain Layer

Responsible for:

- Business rules
- Domain entities
- Value objects
- Business invariants
- Domain services

This layer must remain independent of frameworks and infrastructure.

### Infrastructure Layer

Responsible for:

- Persistence
- External integrations
- File storage
- Email
- Background processing
- Caching
- Technical implementations

## DEPENDENCY RULES

The architecture follows strict dependency direction.

- Dependencies always point inward.
- Outer layers depend on inner layers.
- Inner layers never depend on outer layers.
- Business logic must not depend on implementation details.
- Prefer abstractions over concrete implementations.
- Circular dependencies are prohibited.

## CROSS-CUTTING CONCERNS

The following concerns are centralized and shared across the application:

- Logging
- Validation
- Exception Handling
- Configuration
- Monitoring
- Caching
- Auditing

Business modules must not duplicate these capabilities.

## UNIFIED LOGIN (task 206, resolved 2026-08-02)

Before this, `customer-web`, `admin-web` and `provider-web` each had an
independent `/login` at their own origin, with no way to reach the other two
apps from one place. Task 206 asked for "a single login entry point shared
by all three apps, redirecting to the correct app/dashboard based on account
type", and named two candidate approaches to choose between.

**Decision: shared login route calling the right backend per an account-type
selector, not a subdomain gateway issuing role-scoped tokens.**

Reasoning, verified against the actual repository state rather than assumed:

1. **No shared parent domain exists yet.** DEVOPS.md's OPEN DECISIONS still
   lists cloud provider, hosting platform and registry as unresolved — there
   is no production domain for a subdomain-gateway approach
   (`login.nestly.com` issuing a cookie scoped to `.nestly.com`, shared by
   `app.`/`admin.`/`provider.nestly.com`) to be validated against. Even in
   local development the three apps run on unrelated `localhost` ports, not
   subdomains of one parent.
2. **Account type cannot be derived from an identifier alone.**
   `CustomerAuthIdentity`, `AdminUser` and `ProviderAuthIdentity` are three
   independent tables, each with its own uniqueness scope — nothing stops
   the same email/mobile existing in more than one. A gateway that tried to
   auto-detect "which app does this identifier belong to" would need to
   probe all three backends (latency, and an email-enumeration leak across
   systems) and could still be ambiguous. An explicit account-type selector
   sidesteps this entirely.
3. **Every API already authenticates via a Bearer token in the
   `Authorization` header, never a cookie** (see SECURITY.md), and CORS
   credentials are deliberately off. A subdomain-gateway/shared-cookie
   approach would mean reopening that decision (enabling credentialed CORS,
   picking a shared cookie domain) for a feature that doesn't need it.

**Implementation**: `customer-web`'s `/login` gained an account-type
selector (Customer / Admin / Provider). Selecting Admin or Provider still
authenticates directly against `admin-api`/`provider-api` (each keeps issuing
its own independently-audienced token exactly as before — no change to
`JwtOptions`/`AdminJwtOptions`/`ProviderJwtOptions`), then hands the browser
off via a full-page redirect to that app's own origin with the session
carried in the URL fragment (`lib/unified-login-api.ts`), never a query
string — a fragment is never sent to a server, so the token doesn't touch
any access log on the hop, and the receiving `/auth/callback` page
(`admin-web`, `provider-web`) strips it from history the instant it's read.
This is the standard technique for a same-token cross-origin handoff when
there is no shared cookie domain to rely on instead. The only production
config change this required was adding `customer-web`'s origin to
`admin-api`'s and `provider-api`'s `Cors:AllowedOrigins` (`appsettings.*.json`)
— CORS remains credential-less throughout.

`admin-web`'s and `provider-web`'s own `/login` pages are deliberately left
in place, not removed — a bookmarked or direct visit to either app's own
origin must keep working, and there is no reverse proxy/DNS layer yet to
redirect one to the other. Fully retiring them in favor of the shared entry
point is a follow-up once real hosting/subdomain decisions in DEVOPS.md are
made and a proper redirect can be set up at the infrastructure layer instead
of in application code.

## DOMAIN EVENT DISPATCH AND DELIVERY (task 272, resolved 2026-08-07; durability closed by task 294, 2026-08-08)

Phase 16's order tracking adds a family of domain events
(`ProviderAssignmentAcceptedEvent`, `ProviderEnRouteEvent`,
`ProviderArrivedEvent`, `ProviderLocationUpdatedEvent`,
`BookingEtaUpdatedEvent`) whose consumers include both a live SignalR
broadcast and, later, customer notifications. Those two consumers do not
tolerate loss equally, so the delivery guarantee has to be written down
rather than assumed.

**Decision: keep in-process, post-commit MediatR dispatch with no outbox —
and forbid notification triggers from depending on it alone.**

How dispatch actually works today (`DomainEventDispatchInterceptor`, a
`SaveChangesInterceptor`):

1. Aggregates collect events in memory via `AggregateRoot<TId>.RaiseDomainEvent`.
2. `SavedChangesAsync` — i.e. **after** the transaction has committed —
   sweeps `ChangeTracker.Entries<AggregateRoot<Guid>>()`, drains their
   events, and publishes each one through `IPublisher.Publish` in the same
   process, on the same thread, inside the same request.
3. There is **no outbox table, no queue, no retry, and no dead-letter.** An
   event is published exactly once, best-effort, and then it is gone.

Three consequences follow, and none of them are bugs:

- Only aggregate roots are swept. An event raised on a plain `Entity<TId>` is
  collected and silently never dispatched. Task 272 promoted
  `BookingProviderAssignment` and `ProviderLocationPing` to
  `AggregateRoot<Guid>` for exactly this reason; anything else that starts
  raising events must do the same.
- A handler that throws propagates out of `SaveChangesAsync` to the caller
  **after** the data has already been committed. The write succeeded; the
  request reports failure. Handlers must therefore treat their own failure as
  their problem, not the transaction's.
- If the process dies between commit and publish, every event from that save
  is lost with no record that it ever existed.

**A lost tracking broadcast is acceptable.** REST remains the source of
truth for tracking: `GET /api/v1/bookings/{bookingId}/tracking` (task 275)
returns the current status, latest location and ETA from the database, and
the customer-web client re-reads it on connect, on reconnect and on a
polling fallback. A dropped `ProviderLocationUpdated` frame costs the user
at most one stale marker position until the next ping or the next re-read.
Buying durability for that with an outbox would add a table, a dispatcher
and an at-least-once contract to protect data that is superseded seconds
later anyway.

**A lost notification is not acceptable.** "A professional is on the way" is
not re-derivable by the customer from a screen they are not looking at; if
the SMS/push never goes out, nothing else in the system will ever send it.
Hence the rule:

> **A notification trigger must not depend solely on a post-commit domain
> event handler that can throw.** Every customer-facing notification needs a
> durable record of the intent to send it — written in the same transaction
> as the state change that warrants it — and a retry path (a sweep over
> unsent records) that does not depend on the in-process handler having run.
> The post-commit handler may remain as the fast path; it must not be the
> only path.

**The violation was real, and tasks 276 and 295 each widened it.**
`BookingNotificationTriggerHandler` was exactly the shape the rule forbids: a
post-commit `INotificationHandler` that re-read the booking, customer,
provider, device tokens and payment/cancellation/refund rows and then called
`INotificationDispatchService`. `DispatchAsync` itself is careful — it never
throws for an individual channel's send failure — but the repository reads
ahead of it can, and a process death anywhere in that window lost the
notification with nothing left behind to retry from. Task 276 added five
fulfilment triggers and a fifth repository read on the same footing; task 295
moved ProviderAssigned onto `ProviderAssignmentAcceptedEvent` and added
ProviderChanged, taking that handler to three event types. Both said so at the
time rather than implying a guarantee they did not provide.
`ChatNotificationTriggerHandler`, `SupportTicketNotificationTriggerHandler` and
`SubscriptionNotificationTriggerHandler` shared the shape.

### Task 294: what is guaranteed now

The durable-intent record and its sweep exist, and they cover **all four**
handlers — the rule is not one that half the handlers follow.

How it works:

1. `NotificationIntentInterceptor` (a `SaveChangesInterceptor`, running on
   **`SavingChanges`** — pre-commit, unlike its post-commit sibling
   `DomainEventDispatchInterceptor`) reads the domain events on tracked
   aggregate roots and, for each message they owe, adds a `notification_intent`
   row to the *same* `SaveChanges`. The intent therefore commits — or rolls
   back — atomically with the state change that warrants it. Which messages an
   event owes is decided by `NotificationIntentPlanner`, a pure function that
   both the interceptor and the handlers use, so the writer and the sender
   cannot drift apart.
2. Post-commit dispatch is unchanged and remains the fast path. Every dispatch
   in the four handlers is now wrapped in `INotificationIntentCoordinator`,
   which claims the intent with a single conditional `UPDATE` before sending
   and marks it delivered after.
3. `NotificationIntentSweepJob` — a Hangfire recurring job, every two minutes,
   registered from admin-api like the other sweeps — picks up intents that are
   still pending past a grace period, claims them, rehydrates the serialized
   domain event and re-runs **the same handler** through
   `INotificationTriggerHandler`. It addresses the notification handlers
   directly rather than re-publishing through MediatR, because re-publishing a
   `BookingStatusChangedEvent` would also re-run escrow release, referral
   qualification, metrics and auto-assignment, none of which is idempotent.

**The guarantee, stated exactly:**

> A notification whose intent was committed is delivered **at least once**,
> across process restarts and across app instances, up to a bounded number of
> attempts (`NotificationIntentOptions.MaxAttempts`, default 5 — the in-process
> attempt is the first). Deduplication is by a deterministic key,
> `{domainEventId}:{notificationEventType}`, unique in the database, so the
> sweep can never re-send what the in-process path already delivered.

**What is still not guaranteed, and should not be claimed:**

- **Not exactly-once.** The claim is taken before the send, and the intent is
  marked delivered after it. A process death in between leaves the row claimed
  but pending, and the sweep re-sends it once the lease expires. A duplicate
  SMS is the failure mode this design deliberately chooses over silence.
- **Not unbounded retry.** After `MaxAttempts` the intent moves to the terminal
  `Abandoned` state and is never retried again. That state means a customer was
  owed a message and will not get it; it is logged at error level and is the
  thing to alert on.
- **Not a guarantee for events raised on plain `Entity<TId>`.** The intent
  writer sweeps `ChangeTracker.Entries<AggregateRoot<Guid>>()`, exactly as the
  dispatcher does, so an event raised on a non-aggregate is still invisible to
  both. Anything that starts raising events must be an aggregate root.
- **Not a general outbox.** Only the eight domain events in
  `NotificationIntentPlanner`'s registry produce intents. A notification
  dispatched from a path the planner does not know about — `Welcome` at
  registration, the referral and recurring-booking notifications, OTP — still
  has no durable record and is still at-most-once. The coordinator fails open
  and logs a warning when it is asked to deliver a message with no intent
  behind it, so this shows up rather than passing silently, but it is not
  fixed. Extending the guarantee to those paths means adding them to the
  planner and routing their sender through the coordinator.
- **Not durability for tracking broadcasts.** Unchanged and deliberate: a lost
  `ProviderLocationUpdated` frame is still acceptable, for the reasons above.

## DOMAIN DESIGN PRINCIPLES

The domain model should:

- Encapsulate business rules.
- Protect business invariants.
- Express business concepts clearly.
- Remain independent of technical implementation.
- Favor rich domain behavior over anemic models where appropriate.

## SCALABILITY STRATEGY

The architecture supports:

- Horizontal scaling
- Stateless application services
- Independent module evolution
- Efficient resource utilization
- Asynchronous processing for long-running operations

## RELIABILITY PRINCIPLES

The system should be designed for resilience through:

- Fault isolation
- Retry mechanisms
- Graceful degradation
- Health monitoring
- Failure recovery

## ARCHITECTURAL CONSTRAINTS

All development must adhere to the following constraints:

- Preserve module boundaries.
- Maintain layer separation.
- Do not bypass architectural layers.
- Do not introduce tight coupling.
- Do not duplicate business logic.
- Keep architecture simple and maintainable.

## ARCHITECTURE REVIEW CHECKLIST

Before accepting any architectural change, verify:

- Module boundaries are preserved.
- Dependency direction is correct.
- No circular dependencies exist.
- The design is scalable.
- The design is maintainable.
- The solution is testable.
- The architecture remains consistent with established principles.

## OUT OF SCOPE

This document does not define:

- Business requirements
- Functional specifications
- Technology implementation details
- Coding standards
- Database schema
- API contracts
- Security implementation
- Testing strategy
- Deployment process

Refer to the corresponding project documents for these topics.

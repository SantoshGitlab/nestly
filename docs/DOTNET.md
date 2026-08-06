# DOTNET.md

.NET 8 & ASP.NET Core Development Standards

## PURPOSE

This document defines the project-wide standards, conventions, and best practices for developing backend applications using .NET 8 and ASP.NET Core.

It establishes a consistent approach to application structure, framework usage, dependency management, configuration, request processing, error handling, and performance.

This document is the single source of truth for all .NET development standards.

## TARGET PLATFORM

Backend Platform

- .NET 8 LTS
- ASP.NET Core Web API
- C#
- ASP.NET Core Identity

Only approved framework versions should be used unless explicitly upgraded.

## APPLICATION ARCHITECTURE

Backend applications must follow the architecture defined in **ARCHITECTURE.md**.

Responsibilities:

- HTTP Request Processing
- Application Services
- Business Logic Execution
- Infrastructure Integration
- Background Processing

Framework code must never contain business rules.

## SOLUTION STRUCTURE

The solution should be organized into logical projects.

Typical projects include:

- API
- Application
- Domain
- Infrastructure
- Shared Libraries
- Tests

Each project must have a single, clearly defined responsibility.

## DEPENDENCY INJECTION

Use the built-in ASP.NET Core Dependency Injection container.

Guidelines:

- Constructor Injection only
- Register services through centralized extension methods
- Depend on interfaces rather than concrete implementations
- Select the appropriate service lifetime (Singleton, Scoped, Transient)
- Avoid the Service Locator pattern

## CONFIGURATION MANAGEMENT

Application configuration should be managed through:

- appsettings.json
- Environment-specific configuration
- Environment Variables
- User Secrets (Development)
- Options Pattern

Never hardcode configuration values or secrets.

## MIDDLEWARE

Middleware should be centralized and ordered consistently.

Typical middleware responsibilities include:

- Global Exception Handling
- Request Logging
- Authentication
- Authorization
- CORS
- Response Compression
- Request Correlation

Each middleware should have a single responsibility.

## REQUEST PIPELINE

Every request should follow a consistent processing pipeline.

Client

↓

Middleware

↓

Routing

↓

Authentication

↓

Authorization

↓

Validation

↓

Application

↓

Response

## CONTROLLERS

Controllers should remain lightweight.

Responsibilities:

- Receive requests
- Validate input
- Invoke application services
- Return standardized responses

Controllers must never contain business logic.

## MODEL BINDING & VALIDATION

Request models should:

- Use DTOs
- Validate input before processing
- Reject invalid requests immediately
- Return consistent validation responses

Business validation belongs in the application/domain layer.

## ERROR HANDLING

Exception handling must be centralized.

Requirements:

- Consistent error responses
- Structured logging
- No unhandled exceptions
- No sensitive information leakage
- Meaningful client-facing messages

## LOGGING

Logging should be:

- Structured
- Contextual
- Actionable
- Consistent

Log:

- Requests
- Important business events
- Warnings
- Errors
- Background jobs

Avoid logging sensitive information.

## BACKGROUND PROCESSING

Long-running or scheduled operations should execute outside the request pipeline.

Typical scenarios:

- Scheduled Jobs
- Notifications
- Report Generation
- Data Synchronization
- Cleanup Tasks

Background tasks should be idempotent and recoverable.

### Implementation (T018)

Hangfire, hosted in the API process, backed by the application's PostgreSQL
database (it provisions its own `hangfire` schema on first run).

- Configure through the `BackgroundJobs` section (`BackgroundJobOptions`).
- `ServerEnabled` controls whether a process *executes* jobs. The admin API
  runs the server; the consumer API enqueues only. Tests disable it so they
  never drain a shared queue.
- Retry convention is applied globally: bounded attempts with a widening
  backoff (10s, 1m, 5m, 15m, 1h). A job that exhausts them fails rather than
  retrying forever.
- Because a retry re-runs the whole method, **every job must be idempotent**.
- Jobs must accept and honour a `CancellationToken` so shutdown is graceful.
- The dashboard is admin-only and mounted **only** in the admin API. It can
  enqueue and delete jobs, so it must never appear on the customer surface.

## ASYNCHRONOUS PROGRAMMING

Use asynchronous programming for I/O-bound operations.

Guidelines:

- Prefer async/await
- Avoid synchronous blocking
- Propagate CancellationToken where appropriate
- Do not wrap synchronous code unnecessarily

## SERIALIZATION

Use a consistent JSON serialization strategy across the application.

Requirements:

- Consistent property naming
- Predictable date/time handling
- Ignore unnecessary fields
- Version-compatible responses

## FILE HANDLING

File operations should:

- Validate input
- Restrict file types
- Enforce size limits
- Store files securely
- Prevent path traversal

## CACHING

Use application caching where it provides measurable value.

Typical scenarios:

- Reference Data
- Configuration
- Frequently Read Data

Cache invalidation should be deterministic.

### Implementation (T017)

Application code depends on `ICacheService` (Application layer), never on
`IDistributedCache` directly. The backing store is chosen by configuration:
Redis when `Cache:ConnectionString` is set, an in-process store otherwise.

- The in-process fallback exists for local development and tests. It is **not**
  valid for a scaled deployment — each replica would cache independently — so
  deployed environments must configure a Redis endpoint.
- Build every key through `CacheKeys`. Inlining key strings at call sites is
  how writer and invalidator drift apart.
- Entries are always written with a TTL; `Cache:DefaultExpiration` applies when
  a caller does not specify one.
- The cache is advisory. A miss, an unreachable server, or an undeserializable
  payload degrades to the source of truth and is logged, never thrown.
- `GetOrCreateAsync` is cache-aside, not a lock: concurrent misses may each run
  the factory. Do not use it where the factory has side effects.

## EXTERNAL ROUTE ESTIMATES (T266)

Road distance and travel time come from `IRouteEstimateProvider` (Application
layer) — one origin to many destinations, batched into a single call. Both
auto-assignment ranking (T267) and the customer-facing ETA (T271) use it, so
they cannot drift into two different notions of "near".

The implementation is chosen by configuration, exactly like the cache:

- `GoogleMapsRouteEstimateProvider` when `GoogleMaps:ApiKey` is set and
  `GoogleMaps:Enabled` is true — the Google Maps Platform **Routes API**
  (`computeRouteMatrix`). Not the Distance Matrix API: Google made that a
  legacy product on 1 March 2025 and it cannot be enabled on a Cloud project
  that had not already turned it on, so a new deployment could never switch it
  on.
- `SandboxRouteEstimateProvider` otherwise — Haversine × a road-winding factor
  at a fixed average speed. The whole tracking and assignment stack therefore
  runs with no billing account, no key and no network.

Rules this integration must keep:

- **It never throws.** Missing key, HTTP 5xx, `429`, a rejected credential, a
  timeout, an unparseable body, or a single unroutable destination all degrade
  to the sandbox estimate for the destinations affected, and are logged. An
  approximate ETA on a tracking screen is acceptable; a failed booking is not.
  Caller cancellation is the one exception and propagates as usual.
- **The API key is header-only** (`X-Goog-Api-Key`) and appears in no URL, no
  cache key, no log and no exception message. `GoogleMapsOptions` is a class,
  not a record, so its `ToString` cannot print it.
- Legs are cached through `ICacheService` under `CacheKeys.RouteEstimate`,
  which rounds coordinates to `CacheKeys.RouteEstimateCoordinateDecimals` (4
  places, ~11 m) so a moving provider keeps hitting the same entry.
  `GoogleMaps:CacheTtlSeconds` is short because the durations are
  traffic-aware. Only Google-sourced legs are cached — caching a sandbox
  approximation would outlive the outage that produced it.
- Requests are chunked to `GoogleMaps:MaxDestinationsPerCall`, and one
  `EstimateAsync` may not send more than
  `GoogleMaps:MaxDestinationsPerEstimate` destinations to Google in total.
  Destinations past that cap get the sandbox estimate rather than being
  dropped.

### Configuration keys

| Key | Default | Notes |
| --- | --- | --- |
| `GoogleMaps:ApiKey` | *(none)* | **Secret.** Absent ⇒ sandbox. Set via `GoogleMaps__ApiKey` from the secret store, never in `appsettings.json`. |
| `GoogleMaps:Enabled` | `true` | Kill switch — force the sandbox without deleting the key. |
| `GoogleMaps:TimeoutSeconds` | `5` | Per-request HTTP timeout. |
| `GoogleMaps:CacheTtlSeconds` | `60` | Cached-leg TTL; `0` disables caching. |
| `GoogleMaps:MaxDestinationsPerCall` | `25` | Destinations per HTTP request; hard-capped at 100 (Google's tightest element limit). |
| `GoogleMaps:MaxDestinationsPerEstimate` | `100` | Total destinations one estimate may bill for. |
| `RouteEstimates:Sandbox:RoadWindingFactor` | `1.3` | Road length ÷ straight-line length. |
| `RouteEstimates:Sandbox:AverageSpeedKph` | `25` | Average door-to-door driving speed. |

The server-side key must be restricted **by caller IP** and to the **Routes
API** only. It is not the browser key: the customer web app's
`NEXT_PUBLIC_GOOGLE_MAPS_API_KEY` (T280) is public by construction and can only
be protected by an HTTP-referrer restriction, which does nothing for a server
call. Issue two separate keys.

## HEALTH CHECKS

Applications should expose health endpoints for:

- Application Status
- Database Connectivity
- Cache Connectivity
- External Dependencies

Health endpoints should support operational monitoring.

## API DOCUMENTATION

All APIs should expose OpenAPI (Swagger) documentation.

Documentation should remain synchronized with implementation.

## PACKAGE MANAGEMENT

Use only trusted and actively maintained NuGet packages.

Guidelines:

- Minimize dependencies
- Keep packages updated
- Remove unused packages
- Prefer Microsoft-supported libraries where appropriate

## PERFORMANCE GUIDELINES

Applications should:

- Minimize allocations
- Reduce blocking operations
- Optimize I/O
- Avoid unnecessary database calls
- Reuse framework services

Measure performance before optimizing.

## CODE QUALITY

All .NET code should prioritize:

- Readability
- Simplicity
- Maintainability
- Consistency
- Testability

Follow project coding standards for implementation details.

## FRAMEWORK CONSTRAINTS

Do not:

- Place business logic inside Controllers
- Access Infrastructure directly from Presentation
- Hardcode configuration values
- Use static mutable state
- Bypass Dependency Injection
- Catch exceptions without handling them appropriately

## OUT OF SCOPE

This document does not define:

- Business requirements
- System architecture
- Coding conventions
- Database implementation
- API design standards
- Security policies
- Testing strategy
- Deployment process

Refer to the respective project documents for these topics.

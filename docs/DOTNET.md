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

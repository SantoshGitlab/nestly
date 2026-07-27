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

# DATABASE.md

Database Design & PostgreSQL Standards

## PURPOSE

This document defines the database architecture, design principles, standards, and best practices for the Nestly platform.

It establishes a consistent approach for designing, implementing, maintaining, and optimizing the PostgreSQL database.

This document is the single source of truth for all database-related standards.

## DATABASE PLATFORM

Primary Database

- PostgreSQL

Data Access Technologies

- Entity Framework Core (Primary ORM)
- Dapper (Read Optimization)

Database technology should remain consistent unless officially approved.

## DATABASE OBJECTIVES

The database design must ensure:

- Data Integrity
- Consistency
- Performance
- Scalability
- Reliability
- Maintainability
- Security
- Auditability

## DATABASE DESIGN PRINCIPLES

Every database design should follow:

- Normalization where appropriate
- Clear ownership of data
- Referential Integrity
- Minimal redundancy
- Predictable relationships
- High cohesion
- Low coupling

Database structure should model the business domain rather than application implementation.

## DATA ACCESS STRATEGY

### Entity Framework Core

Use EF Core for:

- Create operations
- Update operations
- Delete operations
- Transactions
- Aggregate updates
- Business workflows
- Domain persistence
- Entity relationships
- Migrations

EF Core is the default persistence technology.

### Dapper

Use Dapper only when optimized read performance is required.

Typical scenarios:

- Reporting
- Dashboards
- Analytics
- Search
- Complex joins
- Large result sets
- Read-heavy queries

Do not use Dapper for business transactions.

## SCHEMA DESIGN

The schema should:

- Represent business concepts clearly
- Keep related data together
- Avoid unnecessary duplication
- Support future growth
- Maintain backward compatibility where possible

## TABLE DESIGN

Every table should:

- Represent a single business concept
- Have a primary key
- Use meaningful names
- Include audit information where required
- Avoid unnecessary nullable columns
- Avoid duplicate data

## PRIMARY KEYS

Requirements:

- Every table must have a primary key.
- Primary keys must be immutable.
- Keys should remain stable throughout the lifetime of the record.

## FOREIGN KEYS

Use foreign keys to maintain referential integrity.

Guidelines:

- Define explicit relationships.
- Prevent orphaned records.
- Avoid unnecessary cascading deletes.
- Preserve business consistency.

## INDEXING

Indexes should improve query performance without unnecessary overhead.

Consider indexes for:

- Primary Keys
- Foreign Keys
- Frequently searched columns
- Frequently sorted columns
- Frequently filtered columns
- Unique constraints

Avoid excessive indexing.

Review index usage periodically.

## QUERY DESIGN

Database queries should:

- Be efficient
- Return only required columns
- Avoid unnecessary joins
- Avoid SELECT *
- Use filtering effectively
- Support pagination where appropriate

Optimize queries only after measuring performance.

## TRANSACTIONS

Transactions should:

- Be as short as possible
- Maintain consistency
- Preserve atomicity
- Handle failures correctly

Long-running transactions should be avoided.

## CONCURRENCY

The application should safely handle concurrent operations.

Guidelines:

- Prevent lost updates
- Handle conflicting modifications
- Maintain data consistency

## MIGRATIONS

Schema changes should be managed through controlled migrations.

Guidelines:

- Keep migrations small
- Make migrations reversible where possible
- Review migration scripts before deployment
- Never modify historical migrations already applied in production

## AUDITING

Audit information should be maintained where required.

Typical fields include:

- Created Date
- Created By
- Modified Date
- Modified By

Business requirements determine audit scope.

## SOFT DELETE

Where business requirements require record retention:

- Prefer soft delete
- Preserve historical data
- Exclude deleted records from normal queries

Permanent deletion should be intentional and controlled.

## DATA VALIDATION

Database constraints should enforce:

- Required values
- Uniqueness
- Referential integrity
- Valid relationships

Business validation belongs in the application/domain layer.

## PERFORMANCE

Performance considerations include:

- Efficient indexing
- Optimized queries
- Proper pagination
- Minimal locking
- Reduced network traffic
- Appropriate batching

Measure performance before optimizing.

## SECURITY

The database should:

- Enforce least privilege
- Restrict direct access
- Protect sensitive data
- Use parameterized queries
- Prevent SQL Injection

Sensitive information should never be stored insecurely.

## BACKUP & RECOVERY

The database strategy should support:

- Regular backups
- Point-in-time recovery
- Disaster recovery
- Restore verification

Recovery procedures should be tested periodically.

## DATABASE REVIEW CHECKLIST

Before releasing database changes, verify:

- Schema follows standards.
- Relationships are correct.
- Indexes are appropriate.
- Queries are optimized.
- Transactions are safe.
- Migrations are reviewed.
- Constraints enforce integrity.
- Performance impact is acceptable.
- Security requirements are satisfied.

## OUT OF SCOPE

This document does not define:

- Business requirements
- System architecture
- .NET implementation
- API design
- Coding standards
- Security policies
- Testing strategy
- Deployment process

Refer to the corresponding project documents for these topics.

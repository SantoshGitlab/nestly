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

### Column stamping vs. the audit trail

These are two distinct mechanisms and should not be confused:

- **Column stamping** — `IAuditable` entities get `CreatedOnUtc` /
  `ModifiedOnUtc` populated automatically by `AuditableEntityInterceptor`.
  This records *when a row last changed*, nothing more.
- **The audit trail (T020)** — the `audit_log` table records *who did what to
  which entity, from where*: actor type and id, entity name and id, action,
  before/after values, IP, correlation id, timestamp.

### Writing audit entries

Use `IAuditLogWriter` (Application layer). It enlists the entry in the current
unit of work and does **not** save — the caller's `SaveChangesAsync` commits it
in the same transaction as the change it describes. That is deliberate: a
rolled-back operation must not leave a phantom audit entry, and a committed one
must never be missing its record.

Attribution (actor, IP, correlation id) is resolved from the ambient request by
`IAuditContextProvider`; callers do not supply it and so cannot misattribute an
action. Work with no request — background jobs, tooling — is recorded as the
`System` actor.

`OldValues` / `NewValues` are `jsonb` and hold the changed fields only. Callers
must strip secrets and PII before constructing the entry; the writer cannot know
which fields of an arbitrary entity are sensitive, and the project rule is
absolute: never log passwords, tokens, or PII.

The table is append-only — the entity exposes no mutators. An audit trail that
can be edited after the fact is not an audit trail.

## RECURRING BOOKINGS

Phase 17 (task 296) models a customer's standing instruction to repeat a
booking on a schedule. The governing rule is that **a recurring booking is not
a second kind of booking**: every occurrence a plan produces is an ordinary
`booking` row, created through the same orchestration a customer's own "Book
now" tap uses, carrying a foreign key back to the plan that produced it. There
is no parallel booking model, no second copy of the pricing/serviceability
rules, and nothing downstream (payments, refunds, assignment, tracking,
reviews) needs to know a booking came from a plan in order to work.

### `recurring_booking_plan`

The schedule itself. Written by the customer (create/pause/resume/cancel),
read by the generator. It holds no pricing, payment, or serviceability state.

| Column | Type | Notes |
| --- | --- | --- |
| `id` | `uuid` PK | |
| `customer_id` | `uuid` NOT NULL | FK → `customer`, Restrict |
| `service_id` | `uuid` NOT NULL | FK → `service`, Restrict. The category is reached through the service; it is not duplicated here |
| `city_id` | `uuid` NOT NULL | FK → `city`, Restrict |
| `locality_id` | `uuid` NOT NULL | FK → `locality`, Restrict |
| `address_id` | `uuid` NOT NULL | FK → `customer_address`, Restrict |
| `slot_window_id` | `uuid` NOT NULL | FK → `slot_window`, Restrict. This is the "preferred slot" — a slot window, not a raw wall-clock time |
| `quantity` | `integer` NOT NULL | |
| `frequency` | `varchar(20)` NOT NULL | `Weekly` / `Biweekly` / `Monthly` |
| `recurrence_day_of_week` | `varchar(20)` NULL | Required for Weekly/Biweekly, must be null for Monthly |
| `recurrence_day_of_month` | `integer` NULL | Required (1–31) for Monthly, must be null otherwise |
| `start_date` | `date` NOT NULL | |
| `end_date` | `date` NULL | |
| `occurrence_count` | `integer` NULL | |
| `completed_occurrence_count` | `integer` NOT NULL | Successfully booked occurrences only |
| `next_occurrence_date` | `date` NOT NULL | The generator's cursor |
| `status` | `varchar(20)` NOT NULL | `Active` / `Paused` / `Cancelled` / `Completed` |
| `created_at_utc` | `timestamptz` NOT NULL | |

Indexes: `(status, next_occurrence_date)` — the exact filter the generator's
due-set query uses — plus `customer_id` and the FK indexes.

`recurring_booking_plan_addon` (`id`, `recurring_booking_plan_id`, `add_on_id`,
`quantity`, Cascade) carries the add-on selections the plan repeats onto every
occurrence.

### `recurring_booking_occurrence`

An append-only audit row recording what the generator did for **one scheduled
date**, whatever the outcome. Same discipline as `booking_status_history` and
`wallet_ledger_entry`: no mutators, never rewritten.

| Column | Type | Notes |
| --- | --- | --- |
| `id` | `uuid` PK | |
| `recurring_booking_plan_id` | `uuid` NOT NULL | FK → `recurring_booking_plan`, Cascade |
| `scheduled_date` | `date` NOT NULL | |
| `outcome` | `varchar(30)` NOT NULL | `Booked` / `SkippedSlotUnavailable` / `SkippedOrchestrationRejected` |
| `booking_id` | `uuid` NULL | FK → `booking`, Restrict. Set only when `outcome = Booked` |
| `skip_reason` | `varchar(500)` NULL | Human-readable only — never a raw exception or stack trace |
| `processed_at_utc` | `timestamptz` NOT NULL | |

Indexes: `(recurring_booking_plan_id, scheduled_date)` UNIQUE — this is the
generator's idempotency guard, so a Hangfire retry or an overlapping run
cannot double-book a date — and `booking_id` UNIQUE, so one booking is claimed
by at most one occurrence (Postgres treats every NULL as distinct, so the
skipped rows are unconstrained).

### Where the plan link lives, and why it lives in both places

The link is expressed twice, deliberately. They answer different questions and
neither replaces the other.

- **`booking.recurring_booking_plan_id`** (nullable FK → `recurring_booking_plan`,
  Restrict, indexed) is the forward link and the primary contract. It makes
  *"is this job recurring, and on what frequency?"* answerable from a booking
  row a list query already loaded — no join, no second query, no dependence on
  the audit log. The admin plan view and the provider "recurring" badge both
  read it. Putting the link **only** on the occurrence table would force every
  provider/admin booking list to join through an audit table and then filter
  out its skipped rows, on a hot read path, to answer a yes/no question.
- **`recurring_booking_occurrence`** covers what the column structurally
  cannot: the scheduled dates that produced **no booking at all**. A skipped
  date has no `booking` row to hang off, so the "why did my plan not run on the
  12th" answer, and the generator's idempotency guard, both have to live in
  their own table.

`booking.recurring_booking_plan_id` is a **real** foreign key, unlike
`booking.source_address_id`, `booking.slot_window_id` and
`booking.subscription_id`, which are traceability-only precisely because they
point at mutable catalog/config rows that may be edited or deleted after the
snapshot was taken. A plan is different: it is never hard-deleted — it is
Cancelled or Completed and kept — so `Restrict` can never block a legitimate
operation, and it guarantees the join is never dangling.

The booking stores **no snapshot** of the plan's own fields (frequency,
day-of-week, …). A customer who changes their plan's frequency expects the
badge on their upcoming jobs to reflect the new frequency, so those are read
live through the key rather than frozen at generation time. This is the one
place a booking deliberately does *not* follow the snapshot convention, and it
is safe because none of those fields participate in the price the booking is
contractually bound to.

### Termination and status semantics

- A plan must be bounded by an end date, an occurrence count, or both — an
  unbounded plan would schedule forever with nothing to ever complete it.
- Whichever bound is reached first wins; the plan then moves to `Completed`.
- `Completed` is distinct from `Cancelled`: "delivered everything it promised"
  and "stopped early by a human" are different outcomes for reporting, even
  though both are terminal for scheduling.
- `Cancelled` is a one-way door — a cancelled plan can never be resumed
  (create a new one instead), the same convention `BookingLifecycle` uses for
  its own terminal states.
- A skipped occurrence advances `next_occurrence_date` but does **not**
  increment `completed_occurrence_count`. A supply-side miss is not charged
  against the customer's occurrence budget, so the plan effectively extends by
  one date rather than delivering one fewer visit than promised.
- Monthly recurrence clamps to the actual month length per month (31 → 28/29
  in February) without ratcheting the stored rule down: the requested day of
  month remains the source of truth every month.

### Enum storage

`frequency`, `status` and `outcome` are persisted as **strings**
(`HasConversion<string>()`), matching every other enum column in this schema —
a `varchar` column is readable in a dump and survives a reordered enum.

Over the wire is a different matter: the APIs serialize enums with the default
`System.Text.Json` behaviour, i.e. as **ordinals**. The house rule from Phase
16 therefore applies to all three: values may only ever be **appended**, never
reordered and never inserted into the middle. `RecurringBookingPlanStatus`
already shows the pattern — `Completed` sits after `Cancelled` because it was
added later, not in its "natural" lifecycle position.

## PROVIDER PHOTO AND PROVIDER-SCOPED REVIEWS

Task 293 closes the two gaps that made `BookingProviderSummary.PhotoUrl` and
`.Rating` structurally always-null: there was no photo column anywhere on
`provider`, and `review` was scoped to a **service**, not to a person. The
governing rule is that **a customer must never be shown a number or an image
that is not actually about the professional at their door** — which is why
both halves are schema changes rather than something derived in a response
mapper.

### `provider` — the photo columns

A photo is a *reference* to an already-hosted image, exactly like
`provider_kyc_document.file_ref`, `cms_media.url` and the completion-proof
photo refs. This schema still has no blob storage and these columns do not
introduce one.

| Column | Type | Notes |
| --- | --- | --- |
| `photo_url` | `varchar(2000)` NULL | Absolute http/https URL only — enforced in `Provider.SubmitPhoto`, not just in the request validator, because the value is rendered into an `img src` and a `javascript:`/`data:` reference there is script execution |
| `photo_moderation_status` | `varchar(20)` NULL | `Pending` / `Approved` / `Rejected`. **Null exactly when `photo_url` is** — same both-or-neither discipline as `latitude`/`longitude`/`location_updated_at_utc` on this table |
| `photo_moderated_by_admin_user_id` | `uuid` NULL | Traceability only, deliberately not a FK — same rationale as `review.moderated_by_admin_user_id` |
| `photo_moderated_at_utc` | `timestamptz` NULL | |
| `photo_moderation_note` | `varchar(1000)` NULL | The rejection reason, shown back to the provider so a rejection is actionable rather than a silent disappearance |

Index: `photo_moderation_status`, which is the admin moderation queue's only
filter and would otherwise scan the whole `provider` table on every load of
that screen.

**Moderation is the house standard here, not an extra.** Every other class of
user-supplied content in this schema goes through an admin verdict before it
counts — `provider_kyc_document.verification_status`, `review.status`/
`is_flagged` — so a photo does too. The gate is expressed **once**, as
`Provider.PublicPhotoUrl` (`photo_url` if and only if the status is
`Approved`), and every customer-facing mapper reads that rather than the raw
column. A gate re-implemented per call site is a gate that will eventually be
missed. Replacing an already-approved photo returns it to `Pending` and clears
the previous verdict: otherwise swapping the image after approval would be a
way to publish an unreviewed one under someone else's sign-off.

### `review.provider_id` — and why it is nullable forever

| Column | Type | Notes |
| --- | --- | --- |
| `provider_id` | `uuid` **NULL** | FK → `provider`, `Restrict`. Deleting a provider must not delete the reviews written about them |

Index: `(provider_id, status)` — the exact filter the per-provider aggregate
uses (`IReviewRepository.GetProviderRatingAsync`), which runs on the booking
detail and the polled live-tracking read, so it must not be a scan. The
aggregate counts **`Visible` reviews only**: a hidden review is hidden from
the rating too, or moderation would be cosmetic. A provider with no visible
reviews has **no rating at all** rather than a rating of zero — "new
professional" and "badly rated" must stay distinguishable all the way to the
screen.

Going forward the column is populated at submission time from the booking's
own `assigned_provider_id` (`ReviewService`): the person being rated is the
one who was on the job when the customer rated them, so capturing it then is
what stops a later reassignment moving the answer.

**The column cannot be `NOT NULL`, and this is a permanent property, not a
migration convenience.** Two populations legitimately resolve to no provider:

1. Historic reviews on bookings that were **reassigned** — see below.
2. Any review on a booking that completed without a provider recorded at all.

A null means *not attributable*. Such a review counts towards nobody's rating.

### The backfill's reassignment rule

`booking.assigned_provider_id` names whoever is on the booking **now**, which
on a reassigned booking may be someone who never did the work. Backfilling
straight from it would put a one-star review on the wrong professional — the
single worst outcome this feature can produce.

So the backfill (`AddProviderPhotoAndProviderScopedReviews.BackfillSql`)
attributes a review **only when the booking's assignment history names exactly
one provider**:

```sql
AND NOT EXISTS (
    SELECT 1 FROM booking_provider_assignment AS a
    WHERE a.booking_id = b.id AND a.provider_id <> b.assigned_provider_id
)
```

Any booking that ever involved a second provider leaves its review's
`provider_id` NULL. That covers every reassignment shape — a rejected offer,
an admin swap mid-job, a withdrawal followed by a new assignment — without
having to interpret assignment statuses, because none of those shapes lets us
prove who was standing in the customer's home when the review was written.
**Prefer null over a wrong attribution** is the rule; the loss is a slightly
thinner rating history, and the alternative is blaming the wrong person.

The statement is idempotent (`AND r.provider_id IS NULL`), so a replayed
migration cannot reattribute a review that has since been corrected by hand.
It is also exposed as a constant rather than inlined, so
`ProviderScopedReviewBackfillTests` executes **that exact string** against a
seeded database — unlike `AddProviderNoDoubleBooking`'s exclusion constraint,
which no test can reach. A rule about who gets blamed for a bad review
deserves coverage rather than a hand-check.

### What deliberately was **not** built

No `provider_rating_summary` rollup table. The aggregate is two numbers over
an indexed `(provider_id, status)` lookup, read once per booking detail and
once per tracking snapshot, and only when a provider is actually assigned. A
denormalised rollup would add a second source of truth that moderation
actions, review edits and backfills would all have to keep in step, to save a
query that is already cheap. Revisit only with a measurement showing this
lookup is hot.

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

# Migrations plan (task 9b)

Building on `docs/migrations-audit.md` (task 9a): this maps out the EF Core
configuration each not-yet-mapped domain entity needs before task 9c can
generate a meaningful initial migration.

## Service (`service` table)

Existing fields (`backend/shared/Domain/Service.cs`): `Id`, `CategoryId`,
`Name`, `Description`, `Price`, `IsActive`.

Configuration needed: PK on `Id`; FK column `CategoryId` (indexed, links to
`category` — see task 37a); `Name` required, max 200; `Description` required,
max 2000; `Price` as `decimal(18,2)`; `IsActive` required.

## ServiceFaq (`service_faq` table)

Existing fields: `Id`, `ServiceId`, `Question`, `Answer`.

Configuration needed: PK on `Id`; FK column `ServiceId` (indexed); `Question`
required, max 500; `Answer` required, max 2000.

## ServiceMedia (`service_media` table)

Existing fields: `Id`, `ServiceId`, `Url`.

Configuration needed: PK on `Id`; FK column `ServiceId` (indexed); `Url`
required, max 1000.

## Customer (`customer` table)

Existing fields (`backend/shared/Application/Domain/Customer.cs`): `Id`,
`Mobile`, `Email`, `Name`, `DateOfBirth`, `Address`, `City`, `State`,
`Pincode`, `Country`, `CreatedAt`, `UpdatedAt`, `Status` (enum:
Active/Blocked/Unverified/SoftDeleted, per SRS 23.1 and task 22).

Configuration needed: PK on `Id`; unique index on `Mobile`; unique index on
`Email` (nullable-safe — email is optional for mobile+OTP-only customers per
SRS 11.2.1); `Name` required, max 200; `Status` stored as string (matches the
project's SQLite/no-native-enum convention already documented in sibling
projects, and keeps the column human-readable in Postgres too); free-text
address fields (`Address`/`City`/`State`/`Pincode`/`Country`) each max 200.

This table alone does not cover task 21's full scope — `customer_auth_identity`,
`customer_session`, and `customer_otp` are new entities that don't exist as
classes yet and are out of scope for this migrations-readiness pass; task 21
covers designing and adding those.

## Sequencing for task 9c

1. Add `ServiceConfiguration`, `ServiceFaqConfiguration`, `ServiceMediaConfiguration`.
2. Add a `CustomerConfiguration` for the existing `Customer` class (namespace
   is currently `Nestly.Application`, not `Nestly.Domain` like every other
   entity — the configuration must reference the actual namespace as-is;
   fixing that inconsistency is out of scope here).
3. Only then run `dotnet ef migrations add InitialCreate -o database/migrations`
   from the `Infrastructure` project (the one wired to `NestlyDbContext`),
   so the generated migration reflects the complete-so-far model rather than
   a partial one that would need regenerating.

# Migrations audit (task 9a)

Audited against the actual codebase on 2026-07-27: `database/migrations/`
contains only `.gitkeep` — no EF Core migration has ever been generated for
this project, and the apply script has nothing to apply.

`NestlyDbContext` discovers entity configurations via
`modelBuilder.ApplyConfigurationsFromAssembly(...)`, so only entities that
have an `IEntityTypeConfiguration<T>` are actually part of the EF model. An
entity class existing under `Nestly.Domain` is not sufficient on its own.

## Domain entity classes found (`backend/shared/Domain/`)

| Entity | Has `IEntityTypeConfiguration<T>`? | In EF model? |
| --- | --- | --- |
| `Category` | Yes (`CategoryConfiguration`) | Yes |
| `Service` | No | **No** |
| `ServiceAddOn` | Yes (`ServiceAddOnConfiguration`) | Yes |
| `ServiceFaq` | No | **No** |
| `ServiceMedia` | No | **No** |
| `Customer` | No | **No** |
| `SupportTicketComment` | Yes (`SupportTicketCommentConfiguration`) | Yes |

No `customer_auth_identity`, `customer_session`, `customer_otp`,
`customer_address`, `audit_log`, or `service_price` entity classes exist at
all yet (see tasks 20, 21, 29 for those).

## Conclusion

Before any migration can be generated (task 9c), every entity above marked
"No" needs an `IEntityTypeConfiguration<T>` (task 9b maps out exactly which
tables/columns each one needs, based on `docs/SRS.md`), and the entities
required by tasks 20/21/29 need to exist as domain classes with
configurations of their own. Only once the model is complete does
`dotnet ef migrations add` produce something meaningful — running it against
today's partial model would under-represent the schema and have to be
redone.

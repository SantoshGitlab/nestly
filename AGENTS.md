# Nestly — agent workspace instructions

Nestly is a .NET 8 / ASP.NET Core home-services marketplace (modular
monolith, Clean Architecture/DDD). See `docs/PROJECT.md`, `docs/ARCHITECTURE.md`,
`docs/SRS.md`, `docs/API.md`, `docs/DATABASE.md`, `docs/CODING-STANDARDS.md`
for the full spec.

## Task workflow

The backlog lives in **`tasks.csv`** at the repo root (columns:
`id,task,status,priority,notes`). This is the live, authoritative task list —
`docs/tasks.csv` is a richer-schema mirror that is currently stale and should
not be trusted over the root file.

For each task you work on:

1. Pick a `todo` row whose dependencies (see the `notes` column, "depends on
   #N") are already `done`. **The task id is the literal string in the `id`
   column** (e.g. `40a`, `40b` — decomposed subtasks use lettered suffixes).
   It is NOT the same as the file's line number — row `40a` happens to sit on
   line 41 of `tasks.csv` (line 1 is the header) purely because rows 1-39
   aren't decomposed, but never report or use a line number as if it were the
   id. When reading the file with a line-numbering tool, strip the line-number
   prefix before quoting or referencing the id.
2. Read the `notes` column carefully — many rows have a note like "reset:
   previously auto-marked done by unverified local-model automation... needs
   real implementation" or point at `_salvage/` for a relevant draft. A
   previous automation run left useful (but misplaced, wrong-namespace) draft
   code in `_salvage/` — read `_salvage/README.md` before implementing
   anything in the Booking, Slots, Support/Notifications, or
   auth/rate-limiting areas; there may already be a usable starting point
   there that just needs the namespace fixed to match the real convention.
3. Implement the task, matching the existing codebase's conventions exactly:
   namespace `Nestly.Domain` / `Nestly.Infrastructure.Persistence.Configurations`
   / etc. (file-scoped namespaces), `Entity<Guid>` base type from
   `Nestly.BuildingBlocks.Primitives`, and EF Core configurations are
   auto-discovered via `modelBuilder.ApplyConfigurationsFromAssembly(...)` in
   `NestlyDbContext` — never manually register a configuration.
4. **Verify with `dotnet build` and `dotnet test`** from the repo root (or the
   relevant `.sln`/project) before marking anything done. Do not use `npm run
   build` — that was the previous automation's bug; this is a .NET project,
   `npm` is only relevant inside `frontend/customer-web` and
   `frontend/admin-web`.
5. Only after a real, passing `dotnet build` (and `dotnet test` where tests
   exist) should you update the task's `status` to `done` in `tasks.csv`.
   Never mark a task done on the model's own assessment alone — that's
   exactly what broke this project's task list before.
6. Commit your work with a clear message referencing the task id.

## Known project state (audited against actual code, not assumed from status)

Only tasks 1-8 and 10-16 are genuinely done: solution wiring, BuildingBlocks
primitives (Result/Error/Entity/AggregateRoot/ValueObject), the exception
middleware, Serilog, the FluentValidation pipeline, DbContext/Npgsql wiring,
health checks, API versioning/OpenAPI, both Dockerfiles, docker-compose, CI,
and both frontend scaffolds.

**Everything else is genuinely `todo`**, including several tasks that were
initially (wrongly) trusted because they had no suspicious automation
annotation — an audit against the actual filesystem found they were never
implemented at all: no migrations were ever generated (task 9), no Redis/
Hangfire/Options-pattern/audit-log/OTP-implementation/JWT/controllers exist
anywhere in `backend/` (tasks 17-31), no auth/profile screens exist in the
frontend (tasks 32-33, though address screens do exist with no backend to
call — task 34), no tests exist anywhere (task 35), the API contracts doc is
a 277-byte stub (task 36), the Catalog/Geography/Serviceability/Slot schemas
were never actually mapped into the DB model (tasks 37-39, 43-44 — Service/
ServiceAddOn/ServiceFaq/ServiceMedia exist as classes but have zero EF
configurations, so they aren't part of the database model yet either).

**Lesson for whoever works this backlog next: a task's `status` column is not
evidence.** Before treating any `done` task as a safe foundation to build on,
grep for the concrete artifact it claims (the entity class, the EF
configuration, the controller, the test file) rather than trusting the CSV.
That is exactly the assumption that let ~24 false-`done` tasks slip through
the first correction pass on this file.

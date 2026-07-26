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
   #N") are already `done`.
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

## Known project state (as of this cleanup)

Tasks 1-39 are trustworthy pre-existing "done" work (solution scaffolding,
building blocks, database/EF setup, DevOps, Identity/Customer modules,
Catalog schema) — verified present under `backend/shared/`. Everything from
task 40 onward was touched by a broken prior automation and has been reset to
`todo` pending real (re-)verification; some of that work may already be
correct and just need a quick `dotnet build` confirmation rather than a full
rewrite — check before reimplementing from scratch.

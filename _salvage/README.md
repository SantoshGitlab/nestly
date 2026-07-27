# Salvaged drafts from the broken autopilot run

These files were recovered from junk directories (`1. **backend`, `2. backend`,
etc.) created by a previous automated worker (aider + local Ollama models) that
misparsed its own LLM output as literal file paths. The junk directories have
been removed; these specific files were kept because they contain genuine,
non-placeholder logic with no equivalent anywhere in the real `backend/` tree.

**None of this is wired into the real project yet.** Every file here needs a
human/agent integration pass before use. Known issues, common to all of them:

- **Wrong namespace.** These use `backend.shared.Application.Domain`,
  `backend.shared.Domain`, `backend.shared.Infrastructure.Persistence.*` etc.
  The real codebase's convention (see `backend/shared/Domain/*.cs`) is
  file-scoped `namespace Nestly.Domain;` / `namespace Nestly.Infrastructure.Persistence.Configurations;`
  with `using Nestly.BuildingBlocks.Primitives;` for the `Entity<T>` base type.
  Rewrite the namespace/using lines before moving a file into `backend/`.
- **`NestlyDbContext` uses assembly scanning.** The real `OnModelCreating`
  calls `modelBuilder.ApplyConfigurationsFromAssembly(typeof(NestlyDbContext).Assembly)` —
  every `IEntityTypeConfiguration<T>` is auto-discovered. Do NOT manually
  register configurations in `OnModelCreating`; just drop the config class in
  `Infrastructure/Persistence/Configurations/` and it's picked up automatically.

## booking/
`Booking.cs`, `BookingItem.cs`, `BookingAddonItem.cs`, `BookingStatusHistory.cs`
— plain entity classes extending `Entity<Guid>`, reasonably complete, no real
counterpart exists in `backend/` yet. Relevant to task 55 (Booking schema) and
related booking-domain tasks. `Slot.cs` (see below) is missing its
`using Nestly.BuildingBlocks.Primitives;` line — will not compile without it.

## slots/
`Slot.cs`, `SlotConfiguration.cs` — relevant to tasks 44/45a-45d (slot rules
schema + slot engine). Both files are missing their `using` directives for
`Nestly.BuildingBlocks.Primitives` (Slot.cs) and
`Microsoft.EntityFrameworkCore` / `Microsoft.EntityFrameworkCore.Metadata.Builders`
(SlotConfiguration.cs) — add these when integrating.

## support-notifications/
`SupportTicketConfiguration.cs`, `NotificationEventConfiguration.cs` — these
are EF Core `IEntityTypeConfiguration` classes, despite the original filenames
suggesting they were entity classes. **The `SupportTicket` and
`NotificationEvent` domain entities themselves do not exist yet anywhere** —
these configs reference entity properties (`Customer.SupportTickets`,
`Customer.NotificationEvents` nav collections, etc.) that will need to be
created first. Relevant to task 84 (Support/Experience schema).

## auth-otp-drafts/
`login_attempt.cs`, `login_attempt_repository.cs`, `rate_limiting_service.cs`,
`otp_service_tests.cs` — complete, no placeholder comments, no equivalent
anywhere in `backend/`. Likely relevant to hardening the existing OTP/login
flow (tasks 23/26 area) with rate limiting and login-attempt tracking that the
real `backend/shared/Domain/OTPService.cs` doesn't currently have.
`customer_service_draft.cs` is an alternate `CustomerService` implementation
that wires in `IOTPService`/`IRateLimitingService` (the real
`backend/shared/Application/Domain/CustomerService.cs` doesn't) but its method
body is truncated with a literal `// ... rest of the code` placeholder — lower
value, keep only as a reference for the constructor/dependency shape.

## Discarded (not salvaged)
Multiple duplicate attempts at `ServiceFaqRepository.cs`, `ServiceMediaRepository.cs`,
`ServiceAddOnRepository.cs` were found across the junk dirs — all versions
were truncated stubs ending in `// ... rest of the code` with no real logic,
so none were kept. Files that duplicated content already properly implemented
in `backend/` (`Service.cs`, `ServiceAddOn.cs`, `ServiceFaq.cs`, `ServiceMedia.cs`,
`SupportTicketComment.cs`, `SupportTicketCommentConfiguration.cs`,
`CustomerValidator.cs`, `OTPService.cs`, a `ValidationResult.cs` stub, and a
`DbContext.cs` fragment showing manual config registration) were also
discarded as superseded/incorrect-pattern duplicates.

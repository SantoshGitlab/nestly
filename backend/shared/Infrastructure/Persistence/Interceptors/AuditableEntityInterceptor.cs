using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Populates audit columns on <see cref="IAuditable"/> entities at save time.
/// </summary>
public sealed class AuditableEntityInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ApplyAuditInformation(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ApplyAuditInformation(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void ApplyAuditInformation(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        DateTime utcNow = DateTime.UtcNow;

        foreach (var entry in context.ChangeTracker.Entries<IAuditable>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedOnUtc = utcNow;
                    break;
                case EntityState.Modified:
                    entry.Entity.ModifiedOnUtc = utcNow;
                    break;
            }
        }
    }
}

using Nestly.Domain;

namespace Nestly.Application;

/// <summary>
/// Persistence for a provider's dispatch capacity limits (PROVIDER.md
/// "provider_capacity", task 245). No admin/provider-facing write path exists
/// yet anywhere in this codebase (confirmed while building task 245 - the
/// entity and its EF configuration were the only things that existed) - every
/// provider effectively has unlimited capacity today, which is a real,
/// honestly-documented gap, not something this repository's read-only shape
/// papers over. Read-only for the same reason: task 245's automatic-assignment
/// gate only needs to consult a limit if one is ever set by a future task.
/// </summary>
public interface IProviderCapacityRepository
{
    Task<ProviderCapacity?> GetByProviderAsync(Guid providerId);
}

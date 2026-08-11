using Nestly.Domain;

namespace Nestly.Application;

/// <summary>
/// Persistence for a provider's dispatch capacity limits (PROVIDER.md
/// "provider_capacity", task 245; write path added task 308). Task 245's
/// automatic-assignment gate (<c>IProviderAssignmentEligibilityService</c>)
/// reads this to hard-enforce <c>MaxJobsPerDay</c>/<c>MaxJobsPerSlot</c>;
/// <see cref="UpsertAsync"/> is the admin-facing write path that lets an
/// admin actually set those limits instead of every provider being
/// permanently unlimited.
/// </summary>
public interface IProviderCapacityRepository
{
    Task<ProviderCapacity?> GetByProviderAsync(Guid providerId);

    /// <summary>
    /// Creates or replaces the one <see cref="ProviderCapacity"/> row for a
    /// provider (unique index on <c>ProviderId</c> - one row per provider,
    /// no separate create/delete verbs needed).
    /// </summary>
    Task UpsertAsync(ProviderCapacity capacity);
}

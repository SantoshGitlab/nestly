using Nestly.Domain;

namespace Nestly.Application.Subscriptions;

public interface ISubscriptionPlanRepository
{
    Task<SubscriptionPlan?> GetByIdAsync(Guid id);

    /// <summary>Case-sensitive exact match against the plan's persisted (already-trimmed) name - used to reject duplicate plan names on create (task 180).</summary>
    Task<bool> NameExistsAsync(string name);

    Task AddAsync(SubscriptionPlan plan);

    Task UpdateAsync(SubscriptionPlan plan);

    /// <summary>Admin plan list (task 180) - every plan, active and inactive, newest first.</summary>
    Task<IReadOnlyList<SubscriptionPlan>> ListAllAsync();

    /// <summary>Customer-facing plan browse (task 181) - only plans currently open to new subscribers.</summary>
    Task<IReadOnlyList<SubscriptionPlan>> ListActiveAsync();
}

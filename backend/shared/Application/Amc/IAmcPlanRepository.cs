using Nestly.Domain;

namespace Nestly.Application.Amc;

public interface IAmcPlanRepository
{
    Task<AmcPlan?> GetByIdAsync(Guid id);

    /// <summary>Case-sensitive exact match against the plan's persisted (already-trimmed) name - used to reject duplicate plan names on create, mirroring <c>ISubscriptionPlanRepository.NameExistsAsync</c>.</summary>
    Task<bool> NameExistsAsync(string name);

    Task AddAsync(AmcPlan plan);

    Task UpdateAsync(AmcPlan plan);

    /// <summary>Admin plan list - every plan, active and inactive, newest first.</summary>
    Task<IReadOnlyList<AmcPlan>> ListAllAsync();

    /// <summary>Customer-facing plan browse - only plans currently open to new purchases.</summary>
    Task<IReadOnlyList<AmcPlan>> ListActiveAsync();
}

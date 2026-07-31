using Nestly.Domain;

namespace Nestly.Application;

public interface ISlotBlackoutRepository : IRepository<SlotBlackout>
{
    /// <summary>Blackouts for a city that overlap the given date range at all, active or not.</summary>
    Task<IReadOnlyList<SlotBlackout>> ListInRangeAsync(Guid cityId, DateOnly from, DateOnly to);

    /// <summary>All blackouts, optionally scoped to a city, newest start date first (task 113b).</summary>
    Task<IReadOnlyList<SlotBlackout>> ListAsync(Guid? cityId);

    Task DeleteAsync(SlotBlackout entity);
}

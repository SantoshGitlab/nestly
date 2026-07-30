using Nestly.Domain;

namespace Nestly.Application;

public interface ISlotBlackoutRepository : IRepository<SlotBlackout>
{
    /// <summary>Blackouts for a city that overlap the given date range at all, active or not.</summary>
    Task<IReadOnlyList<SlotBlackout>> ListInRangeAsync(Guid cityId, DateOnly from, DateOnly to);
}

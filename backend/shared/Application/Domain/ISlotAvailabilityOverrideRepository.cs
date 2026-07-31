using Nestly.Domain;

namespace Nestly.Application;

/// <summary>Persistence for one-off availability overrides (SRS 12.10.2, task 113e).</summary>
public interface ISlotAvailabilityOverrideRepository : IRepository<SlotAvailabilityOverride>
{
    /// <summary>Overrides, optionally scoped to a city and/or a date, newest date first.</summary>
    Task<IReadOnlyList<SlotAvailabilityOverride>> ListAsync(Guid? cityId, DateOnly? date);

    Task DeleteAsync(SlotAvailabilityOverride entity);
}

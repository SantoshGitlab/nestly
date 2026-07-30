using Nestly.Domain;

namespace Nestly.Application;

public interface ISlotWindowRepository : IRepository<SlotWindow>
{
    /// <summary>Active windows for a city that have a rule for the given day of week, ordered by start time.</summary>
    Task<IReadOnlyList<SlotWindow>> ListActiveForCityAndDayAsync(Guid cityId, DayOfWeek dayOfWeek);
}

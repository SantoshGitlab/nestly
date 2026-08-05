using Nestly.Domain;

namespace Nestly.Application;

public interface ISlotWindowRepository : IRepository<SlotWindow>
{
    /// <summary>Active windows for a city that have a rule for the given day of week, ordered by start time.</summary>
    Task<IReadOnlyList<SlotWindow>> ListActiveForCityAndDayAsync(Guid cityId, DayOfWeek dayOfWeek);

    /// <summary>All windows (active and inactive), optionally scoped to a city, ordered by city name then start time (task 113a).</summary>
    Task<IReadOnlyList<SlotWindow>> ListAsync(Guid? cityId);

    /// <summary>The days of week a window is currently offered on.</summary>
    Task<IReadOnlyList<DayOfWeek>> ListRuleDaysAsync(Guid slotWindowId);

    /// <summary>Rule days for a set of windows in one round trip (task 256) - the admin window list rendered them a window at a time. Windows with no rules are absent from the result.</summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<DayOfWeek>>> ListRuleDaysByWindowIdsAsync(IReadOnlyCollection<Guid> slotWindowIds);

    /// <summary>Names for a set of window ids in one round trip (task 256) - the admin override list renders a window name per row.</summary>
    Task<IReadOnlyDictionary<Guid, string>> GetNamesByIdsAsync(IReadOnlyCollection<Guid> ids);

    /// <summary>Replaces a window's day-of-week rules wholesale with the given set (task 113a).</summary>
    Task ReplaceRulesAsync(Guid slotWindowId, IReadOnlyList<DayOfWeek> daysOfWeek);
}

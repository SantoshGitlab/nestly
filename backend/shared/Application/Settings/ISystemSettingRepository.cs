using Nestly.Domain;

namespace Nestly.Application.Settings;

/// <summary>Persistence for the <see cref="SystemSetting"/> group rows (SRS 12.19, tasks 131a-131h).</summary>
public interface ISystemSettingRepository
{
    /// <summary>Fetches a group's current row by its <see cref="SystemSetting.GroupKey"/> (see <see cref="SystemSettingGroups"/>).</summary>
    Task<SystemSetting?> GetByGroupKeyAsync(string groupKey, CancellationToken cancellationToken = default);

    /// <summary>Every settings group row, for the admin Settings landing page (task 131h).</summary>
    Task<IReadOnlyList<SystemSetting>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Persists a change to an already-loaded group row.</summary>
    Task UpdateAsync(SystemSetting setting, CancellationToken cancellationToken = default);
}

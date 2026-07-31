using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>
/// One admin-configurable settings group (SRS 12.19, tasks 131a-131h): a
/// group key (see <see cref="SystemSettingGroups"/>) plus its current value
/// as a JSON payload. Deliberately generic - a single table backs every
/// settings group instead of one bespoke table per group, with the group's
/// C# record type (in <c>Application/Settings/SettingsContracts.cs</c>) as
/// the actual source of typed structure. <see cref="ISystemSettingsService"/>
/// is what gives callers a typed accessor per group; this entity only knows
/// about opaque JSON.
/// </summary>
/// <remarks>
/// One row exists per group, seeded by the <c>AddSystemSettings</c>
/// migration - admins edit an existing group's value, they never create or
/// delete a group. <see cref="GroupKey"/> is therefore unique but not the
/// primary key, so foreign keys and the audit trail can still reference a
/// stable <see cref="Entity{TId}.Id"/> even if a group were ever renamed.
/// </remarks>
public class SystemSetting : Entity<Guid>
{
    /// <summary>One of <see cref="SystemSettingGroups"/>. Unique.</summary>
    public string GroupKey { get; private set; } = string.Empty;

    /// <summary>Current value, serialized as JSON matching the group's settings record shape.</summary>
    public string ValueJson { get; private set; } = string.Empty;

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    /// <summary>Admin who last changed this group's value; null if never changed since seeding.</summary>
    public Guid? UpdatedByAdminUserId { get; private set; }

    protected SystemSetting() { }

    /// <summary>Constructs a seeded group row with its initial default value.</summary>
    public SystemSetting(Guid id, string groupKey, string valueJson) : base(id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(valueJson);

        GroupKey = groupKey;
        ValueJson = valueJson;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
    }

    /// <summary>Replaces this group's value, attributing the change to the admin who made it.</summary>
    public void UpdateValue(string valueJson, Guid? updatedByAdminUserId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(valueJson);

        ValueJson = valueJson;
        UpdatedAtUtc = DateTime.UtcNow;
        UpdatedByAdminUserId = updatedByAdminUserId;
    }
}

using Nestly.Domain;

namespace Nestly.Application;

/// <summary>Persistence for <see cref="PartnerBackgroundCheck"/> (task 160).</summary>
public interface IPartnerBackgroundCheckRepository
{
    Task AddAsync(PartnerBackgroundCheck entity);

    /// <summary>Full check history for a partner, newest first.</summary>
    Task<IReadOnlyList<PartnerBackgroundCheck>> ListByPartnerAsync(Guid partnerId);

    /// <summary>The most recent check outcome, or null if none was ever recorded (treated as still Pending). Used by the Partner activation gate.</summary>
    Task<PartnerBackgroundCheck?> GetLatestByPartnerAsync(Guid partnerId);
}

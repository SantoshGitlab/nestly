using Nestly.Domain;

namespace Nestly.Application.Referral;

public interface IReferralMilestoneRepository
{
    /// <summary>Every active milestone, ascending by threshold - the order they should be checked in as a referrer's count climbs.</summary>
    Task<IReadOnlyList<ReferralMilestone>> ListActiveOrderedByThresholdAsync();

    /// <summary>Every milestone (active or not), for the admin view.</summary>
    Task<IReadOnlyList<ReferralMilestone>> ListAllOrderedByThresholdAsync();

    Task<ReferralMilestone?> GetByIdAsync(Guid id);

    Task<bool> ExistsByThresholdAsync(int thresholdCount);

    Task AddAsync(ReferralMilestone milestone);

    Task UpdateAsync(ReferralMilestone milestone);
}

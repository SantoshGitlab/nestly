using Nestly.Domain;

namespace Nestly.Application.Referral;

public interface IReferralMilestoneAwardRepository
{
    /// <summary>Task 174's idempotency guard: has this referrer already received this milestone's bonus?</summary>
    Task<bool> ExistsAsync(Guid referralMilestoneId, Guid referrerCustomerId);

    Task AddAsync(ReferralMilestoneAward award);

    /// <summary>Task 171's cost report: milestone bonuses awarded within the range.</summary>
    Task<IReadOnlyList<ReferralMilestoneAward>> ListInRangeAsync(DateTime? fromUtc, DateTime? toUtc);
}

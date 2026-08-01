using Nestly.Domain;

namespace Nestly.Application.Referral;

public interface IReferralRepository
{
    Task<Domain.Referral?> GetByIdAsync(Guid id);

    /// <summary>Task 163's self-referral/duplicate-referee guard, and task 164's qualifying-booking lookup key off the referee.</summary>
    Task<Domain.Referral?> GetByRefereeCustomerIdAsync(Guid refereeCustomerId);

    Task<IReadOnlyList<Domain.Referral>> ListByReferrerCustomerIdAsync(Guid referrerCustomerId);

    /// <summary>Task 166/165's per-customer reward cap (REFERRAL.md "FRAUD / ABUSE PREVENTION").</summary>
    Task<int> CountRewardedByReferrerAsync(Guid referrerCustomerId);

    /// <summary>Task 175's expiry sweep: Registered rows whose ExpiresAtUtc has passed.</summary>
    Task<IReadOnlyList<Domain.Referral>> ListExpiredAsync(DateTime asOfUtc);

    Task AddAsync(Domain.Referral referral);

    Task UpdateAsync(Domain.Referral referral);
}

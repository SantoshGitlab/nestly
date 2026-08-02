using Nestly.Domain;

namespace Nestly.Application.Referral;

public interface IReferralRepository
{
    Task<Domain.Referral?> GetByIdAsync(Guid id);

    /// <summary>Task 163's self-referral/duplicate-referee guard, and task 164's qualifying-booking lookup key off the referee.</summary>
    Task<Domain.Referral?> GetByRefereeCustomerIdAsync(Guid refereeCustomerId);

    /// <summary>Task 166's post-reward cancellation signal: is this booking a Rewarded referral's qualifying booking?</summary>
    Task<Domain.Referral?> GetByQualifyingBookingIdAsync(Guid bookingId);

    Task<IReadOnlyList<Domain.Referral>> ListByReferrerCustomerIdAsync(Guid referrerCustomerId);

    /// <summary>Task 166/165's per-customer reward cap (REFERRAL.md "FRAUD / ABUSE PREVENTION").</summary>
    Task<int> CountRewardedByReferrerAsync(Guid referrerCustomerId);

    /// <summary>Task 175's expiry sweep: Registered rows whose ExpiresAtUtc has passed.</summary>
    Task<IReadOnlyList<Domain.Referral>> ListExpiredAsync(DateTime asOfUtc);

    /// <summary>
    /// Task 170's admin list: optional status/fraud-flag filters, optional
    /// restriction to a set of customer ids (either side) for the admin's
    /// "search by customer" box - resolved to ids by the caller via
    /// <c>ICustomerRepository.SearchAsync</c> first, since Referral itself
    /// carries no denormalized customer name/mobile to search against
    /// directly.
    /// </summary>
    Task<(IReadOnlyList<Domain.Referral> Items, int TotalCount)> SearchAsync(
        ReferralStatus? status, bool? isFraudFlagged, IReadOnlyList<Guid>? customerIds, int page, int pageSize);

    /// <summary>Task 171's funnel report: referrals registered within the range (cohort basis - "of those who registered in this window, how many progressed").</summary>
    Task<IReadOnlyList<Domain.Referral>> ListRegisteredInRangeAsync(DateTime? fromUtc, DateTime? toUtc);

    /// <summary>Task 171's cost report: referrals rewarded within the range.</summary>
    Task<IReadOnlyList<Domain.Referral>> ListRewardedInRangeAsync(DateTime? fromUtc, DateTime? toUtc);

    Task AddAsync(Domain.Referral referral);

    Task UpdateAsync(Domain.Referral referral);
}

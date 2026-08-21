using Nestly.Domain;

namespace Nestly.Application.ProviderReferral;

public interface IProviderReferralRepository
{
    Task<Nestly.Domain.ProviderReferral?> GetByIdAsync(Guid id);

    /// <summary>Self-referral/duplicate-referee guard at registration, and the qualifying-job lookup key off the referee.</summary>
    Task<Nestly.Domain.ProviderReferral?> GetByRefereeProviderIdAsync(Guid refereeProviderId);

    /// <summary>Post-reward cancellation signal: is this booking a Rewarded referral's qualifying booking?</summary>
    Task<Nestly.Domain.ProviderReferral?> GetByQualifyingBookingIdAsync(Guid bookingId);

    Task<IReadOnlyList<Nestly.Domain.ProviderReferral>> ListByReferrerProviderIdAsync(Guid referrerProviderId);

    /// <summary>Per-provider reward cap (PROVIDER-REFERRAL.md "FRAUD / ABUSE PREVENTION").</summary>
    Task<int> CountRewardedByReferrerAsync(Guid referrerProviderId);

    /// <summary>Expiry sweep: Registered rows whose ExpiresAtUtc has passed.</summary>
    Task<IReadOnlyList<Nestly.Domain.ProviderReferral>> ListExpiredAsync(DateTime asOfUtc);

    /// <summary>Admin list: optional status/fraud-flag filters, optional restriction to a set of provider ids (either side) for the admin's "search by provider" box.</summary>
    Task<(IReadOnlyList<Nestly.Domain.ProviderReferral> Items, int TotalCount)> SearchAsync(
        ProviderReferralStatus? status, bool? isFraudFlagged, IReadOnlyList<Guid>? providerIds, int page, int pageSize);

    Task AddAsync(Nestly.Domain.ProviderReferral referral);

    Task UpdateAsync(Nestly.Domain.ProviderReferral referral);
}

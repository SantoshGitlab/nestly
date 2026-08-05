using Nestly.Application.Customers;
using Nestly.BuildingBlocks.Primitives;
using System.Collections.Generic;

namespace Nestly.Application
{
    public interface ICustomerRepository : IRepository<Customer>
    {
        // ExistsAsync(Guid) is inherited from IRepository<Customer> - redeclaring
        // it here shadowed the base member (CS0108) without changing behaviour.
        Task<bool> ExistsByMobileAsync(string mobile);
        Task<bool> ExistsByEmailAsync(string email);
        Task<Customer?> GetByMobileAsync(string mobile);

        /// <summary>Referral code uniqueness check (task 162) - queried before assigning a newly generated candidate code.</summary>
        Task<bool> ExistsByReferralCodeAsync(string referralCode);

        /// <summary>Resolves the referrer at registration time (task 163) from the code the referee entered.</summary>
        Task<Customer?> GetByReferralCodeAsync(string referralCode);

        /// <summary>Search/filter with pagination (SRS 12.4.1, task 101a) - see <see cref="CustomerSearchFilter"/> for the supported criteria.</summary>
        Task<CustomerSearchResult> SearchAsync(CustomerSearchFilter filter);

        /// <summary>
        /// Display names for a page of customer ids, in one round trip (task
        /// 253). Referral list screens render a name per customer id and used
        /// to call <c>GetByIdAsync</c> per row - two calls per row on the
        /// admin list, which loads whole aggregates purely to read one column.
        /// Ids with no matching customer are simply absent from the result,
        /// so callers keep their own "Unknown" fallback.
        /// </summary>
        Task<IReadOnlyDictionary<Guid, string>> GetNamesByIdsAsync(IReadOnlyCollection<Guid> ids);
    }
}

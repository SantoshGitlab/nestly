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
    }
}

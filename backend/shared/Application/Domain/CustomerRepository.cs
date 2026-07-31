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

        /// <summary>Search/filter with pagination (SRS 12.4.1, task 101a) - see <see cref="CustomerSearchFilter"/> for the supported criteria.</summary>
        Task<CustomerSearchResult> SearchAsync(CustomerSearchFilter filter);
    }
}

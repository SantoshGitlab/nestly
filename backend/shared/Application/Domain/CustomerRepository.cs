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
    }
}

using Nestly.BuildingBlocks.Primitives;
using System.Collections.Generic;

namespace Nestly.Application
{
    public interface ICustomerRepository : IRepository<Customer>
    {
        Task<bool> ExistsAsync(Guid id);
        Task<bool> ExistsByMobileAsync(string mobile);
        Task<bool> ExistsByEmailAsync(string email);
    }
}

using System.Collections.Generic;

namespace backend.shared.Application.Domain
{
    public interface ICustomerRepository : IRepository<Customer>
    {
        Task<bool> ExistsAsync(Guid id);
        Task<bool> ExistsByMobileAsync(string mobile);
        Task<bool> ExistsByEmailAsync(string email);
    }
}

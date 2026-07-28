using Nestly.Domain;

namespace Nestly.Application;

public interface ICustomerAuthIdentityRepository
{
    Task AddAsync(CustomerAuthIdentity entity);
    Task UpdateAsync(CustomerAuthIdentity entity);
    Task<CustomerAuthIdentity?> GetByProviderAsync(AuthProviderType provider, string identifier);
    Task<IReadOnlyList<CustomerAuthIdentity>> GetByCustomerAsync(Guid customerId);
    Task<bool> ExistsAsync(AuthProviderType provider, string identifier);
}

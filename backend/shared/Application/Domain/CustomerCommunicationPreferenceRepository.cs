using Nestly.Domain;

namespace Nestly.Application;

public interface ICustomerCommunicationPreferenceRepository
{
    Task AddAsync(CustomerCommunicationPreference entity);
    Task UpdateAsync(CustomerCommunicationPreference entity);
    Task<CustomerCommunicationPreference?> GetByCustomerAsync(Guid customerId);
}

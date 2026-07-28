using Nestly.Domain;

namespace Nestly.Application;

public interface ICustomerSessionRepository
{
    Task AddAsync(CustomerSession entity);
    Task UpdateAsync(CustomerSession entity);
    Task<CustomerSession?> GetByRefreshTokenHashAsync(string refreshTokenHash);
}

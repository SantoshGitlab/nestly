using Nestly.Domain;

namespace Nestly.Application;

public interface ICustomerSessionRepository
{
    Task AddAsync(CustomerSession entity);
    Task UpdateAsync(CustomerSession entity);
    Task<CustomerSession?> GetByRefreshTokenHashAsync(string refreshTokenHash);

    /// <summary>
    /// Revokes every still-active session for a customer and returns how many
    /// were revoked. Used by the password reset flow (SRS 11.2.2) so tokens
    /// issued before the reset stop working the moment the password changes.
    /// </summary>
    Task<int> RevokeAllForCustomerAsync(Guid customerId);
}

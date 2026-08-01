using Nestly.Domain;

namespace Nestly.Application;

public interface IPartnerSessionRepository
{
    Task AddAsync(PartnerSession entity);
    Task UpdateAsync(PartnerSession entity);
    Task<PartnerSession?> GetByRefreshTokenHashAsync(string refreshTokenHash);

    /// <summary>Revokes every still-active session for a partner (mirrors <c>ICustomerSessionRepository.RevokeAllForCustomerAsync</c>).</summary>
    Task<int> RevokeAllForPartnerAsync(Guid partnerId);
}

using Nestly.Domain;

namespace Nestly.Application;

public interface IPartnerAuthIdentityRepository
{
    Task AddAsync(PartnerAuthIdentity entity);
    Task UpdateAsync(PartnerAuthIdentity entity);
    Task<PartnerAuthIdentity?> GetByProviderAsync(AuthProviderType provider, string identifier);
    Task<IReadOnlyList<PartnerAuthIdentity>> GetByPartnerAsync(Guid partnerId);
    Task<bool> ExistsAsync(AuthProviderType provider, string identifier);
}

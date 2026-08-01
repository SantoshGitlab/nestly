using Nestly.Domain;

namespace Nestly.Application;

public interface IPartnerKycDocumentRepository
{
    Task AddAsync(PartnerKycDocument entity);
    Task UpdateAsync(PartnerKycDocument entity);
    Task<PartnerKycDocument?> GetByIdAsync(Guid id);
    Task<IReadOnlyList<PartnerKycDocument>> GetByPartnerAsync(Guid partnerId);
}

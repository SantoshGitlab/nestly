using Nestly.Domain;

namespace Nestly.Application;

/// <summary>
/// Persistence for a partner's declared skills/capabilities (PARTNER.md
/// "partner_skill_mapping", task 149a "update skills"). Replace-all
/// semantics, same reasoning as <see cref="IPartnerServiceAreaRepository"/>.
/// </summary>
public interface IPartnerSkillMappingRepository
{
    Task<IReadOnlyList<PartnerSkillMapping>> GetByPartnerAsync(Guid partnerId);

    Task ReplaceForPartnerAsync(Guid partnerId, IReadOnlyList<PartnerSkillMapping> skills);
}

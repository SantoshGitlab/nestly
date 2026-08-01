using Nestly.Domain;

namespace Nestly.Application;

/// <summary>
/// Persistence for a partner's geography coverage (PARTNER.md
/// "partner_service_area", task 149a "update service areas"). Replace-all
/// semantics rather than individual add/remove, mirroring
/// <c>ISlotWindowRepository.ReplaceRulesAsync</c> — a partner submits their
/// whole coverage set at once, so a full replace avoids reconciling a partial
/// diff against the unique (partner, city, zone, pincode) index.
/// </summary>
public interface IPartnerServiceAreaRepository
{
    Task<IReadOnlyList<PartnerServiceArea>> GetByPartnerAsync(Guid partnerId);

    Task ReplaceForPartnerAsync(Guid partnerId, IReadOnlyList<PartnerServiceArea> areas);
}

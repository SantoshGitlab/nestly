using Nestly.Domain;

namespace Nestly.Application;

/// <summary>
/// Persistence for a partner's blackout dates (PARTNER.md "partner_availability
/// ... blackout dates", task 149b). Individual add/delete rather than
/// replace-all — mirrors <c>ISlotBlackoutRepository</c>, whose city-scoped
/// equivalent this structurally matches.
/// </summary>
public interface IPartnerBlackoutDateRepository
{
    Task<IReadOnlyList<PartnerBlackoutDate>> GetByPartnerAsync(Guid partnerId);

    Task<PartnerBlackoutDate?> GetByIdAsync(Guid id);

    Task AddAsync(PartnerBlackoutDate entity);

    Task DeleteAsync(PartnerBlackoutDate entity);
}

using Nestly.Domain;

namespace Nestly.Application;

/// <summary>
/// Persistence for a partner's recurring weekly availability (PARTNER.md
/// "partner_availability", task 149b). Replace-all semantics, same reasoning
/// as <see cref="IPartnerServiceAreaRepository"/> — a partner submits their
/// whole weekly schedule at once.
/// </summary>
public interface IPartnerAvailabilityWindowRepository
{
    Task<IReadOnlyList<PartnerAvailabilityWindow>> GetByPartnerAsync(Guid partnerId);

    Task ReplaceForPartnerAsync(Guid partnerId, IReadOnlyList<PartnerAvailabilityWindow> windows);
}

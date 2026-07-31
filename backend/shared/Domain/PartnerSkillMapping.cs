using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>
/// A category (and optionally a specific service within it) a partner is
/// qualified/willing to work in (PARTNER.md "partner_skill_mapping: partner_id
/// -> category/service they're qualified for"). <see cref="ServiceId"/> is
/// optional so a partner can declare capability at the broader category level
/// without listing every service in it; when present it narrows the mapping
/// to that one service. Shape mirrors <c>CategoryCityMapping</c>.
/// </summary>
public class PartnerSkillMapping : Entity<Guid>
{
    public Guid PartnerId { get; private set; }
    public Guid CategoryId { get; private set; }
    public Guid? ServiceId { get; private set; }
    public bool IsActive { get; private set; }

    protected PartnerSkillMapping() { }

    public PartnerSkillMapping(Guid id, Guid partnerId, Guid categoryId, Guid? serviceId = null) : base(id)
    {
        PartnerId = partnerId;
        CategoryId = categoryId;
        ServiceId = serviceId;
        IsActive = true;
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}

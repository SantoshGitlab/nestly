using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>
/// A partner's dispatch capacity limits (PARTNER.md "partner_capacity: Max
/// jobs per day/slot, if capacity-based dispatch is used"). One row per
/// partner. Both limits are optional/null = unlimited, mirroring
/// <c>SlotWindow.MaxBookingsPerSlot</c>'s null-is-unlimited convention.
/// Advisory only in v1 - OPEN DECISIONS #1 keeps assignment manual, so
/// nothing enforces these limits automatically yet; an admin can consult
/// them when hand-assigning a booking.
/// </summary>
public class PartnerCapacity : Entity<Guid>
{
    public Guid PartnerId { get; private set; }
    public int? MaxJobsPerDay { get; private set; }
    public int? MaxJobsPerSlot { get; private set; }

    protected PartnerCapacity() { }

    public PartnerCapacity(Guid id, Guid partnerId, int? maxJobsPerDay = null, int? maxJobsPerSlot = null) : base(id)
    {
        PartnerId = partnerId;
        SetLimits(maxJobsPerDay, maxJobsPerSlot);
    }

    public void SetLimits(int? maxJobsPerDay, int? maxJobsPerSlot)
    {
        if (maxJobsPerDay is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxJobsPerDay), "Capacity must be positive when set.");
        }

        if (maxJobsPerSlot is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxJobsPerSlot), "Capacity must be positive when set.");
        }

        MaxJobsPerDay = maxJobsPerDay;
        MaxJobsPerSlot = maxJobsPerSlot;
    }
}

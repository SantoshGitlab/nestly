using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>
/// A date range in which a partner is unavailable (PARTNER.md
/// "partner_availability: ... blackout dates"). Structural mirror of
/// <c>SlotBlackout</c> (a city-level equivalent in the Slot Engine), scoped
/// to one partner instead of one city, and without <c>SlotBlackout</c>'s
/// Holiday/Blackout type discriminator - a partner's own unavailability
/// (leave, illness, personal blackout) has no "named holiday" variant to
/// distinguish.
/// </summary>
public class PartnerBlackoutDate : Entity<Guid>
{
    public Guid PartnerId { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public string? Reason { get; private set; }

    protected PartnerBlackoutDate() { }

    public PartnerBlackoutDate(Guid id, Guid partnerId, DateOnly startDate, DateOnly endDate, string? reason = null)
        : base(id)
    {
        if (startDate > endDate)
        {
            throw new ArgumentException("The start date must not be after the end date.");
        }

        PartnerId = partnerId;
        StartDate = startDate;
        EndDate = endDate;
        Reason = reason;
    }

    /// <summary>Whether this blackout covers the given date, inclusive of both ends.</summary>
    public bool CoversDate(DateOnly date) => date >= StartDate && date <= EndDate;
}

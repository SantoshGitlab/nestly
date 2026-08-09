using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>
/// A provider's dispatch capacity limits (PROVIDER.md "provider_capacity: Max
/// jobs per day/slot, if capacity-based dispatch is used"). One row per
/// provider. Both limits are optional/null = unlimited, mirroring
/// <c>SlotWindow.MaxBookingsPerSlot</c>'s null-is-unlimited convention.
/// Hard-enforced at assignment time by <c>IProviderAssignmentEligibilityService</c>
/// (task 245, PROVIDER.md OPEN DECISIONS - AUTOMATIC ASSIGNMENT #2): a
/// provider already at either limit is filtered out of the automatic
/// engine's candidates entirely, not merely flagged. Manual admin assignment
/// still surfaces the same numbers only as an advisory load signal (an admin
/// can choose to override), the one place these limits remain non-blocking.
/// </summary>
public class ProviderCapacity : Entity<Guid>
{
    public Guid ProviderId { get; private set; }
    public int? MaxJobsPerDay { get; private set; }
    public int? MaxJobsPerSlot { get; private set; }

    protected ProviderCapacity() { }

    public ProviderCapacity(Guid id, Guid providerId, int? maxJobsPerDay = null, int? maxJobsPerSlot = null) : base(id)
    {
        ProviderId = providerId;
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

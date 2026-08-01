using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>
/// One add-on line a <see cref="RecurringBookingPlan"/> repeats onto every
/// occurrence it books. Deliberately not a price/name snapshot the way
/// <see cref="BookingAddOnItem"/> is - a plan re-prices fresh through the
/// booking orchestration (task 58) at each occurrence, so only the add-on's
/// identity and quantity need to survive between occurrences; the price is
/// whatever the catalog says on the day, exactly like every other field the
/// orchestration re-validates rather than trusts from the plan.
/// </summary>
public class RecurringBookingPlanAddOn : Entity<Guid>
{
    public Guid RecurringBookingPlanId { get; private set; }

    public Guid AddOnId { get; private set; }

    public int Quantity { get; private set; }

    protected RecurringBookingPlanAddOn() { }

    public RecurringBookingPlanAddOn(Guid id, Guid recurringBookingPlanId, Guid addOnId, int quantity)
        : base(id)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Add-on quantity must be positive.");
        }

        RecurringBookingPlanId = recurringBookingPlanId;
        AddOnId = addOnId;
        Quantity = quantity;
    }
}

using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>
/// An admin-configured "Nestly Plus"-style membership tier
/// (PRODUCT-ENHANCEMENTS.md #1, tasks 177/180): price, billing cadence, and
/// the benefits a subscriber gets - a number of free visits per cycle, a
/// standing percentage discount, and an optional priority-slot flag. Mirrors
/// <see cref="Coupon"/>'s split: this entity owns only rules that are pure
/// functions of its own fields (construction/edit validation); everything
/// I/O-dependent (name uniqueness, active-subscriber counts) lives in
/// <c>ISubscriptionPlanManagementService</c>.
/// </summary>
public class SubscriptionPlan : Entity<Guid>
{
    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public decimal Price { get; private set; }

    public SubscriptionBillingCycle BillingCycle { get; private set; }

    /// <summary>Number of bookings per billing cycle a subscriber can take fully free (PRODUCT-ENHANCEMENTS.md #1's "free_visits_included"). Zero is valid - a discount-only plan.</summary>
    public int FreeVisitsIncluded { get; private set; }

    /// <summary>Standing percentage discount applied once free visits for the cycle are exhausted (task 179). Zero is valid - a free-visits-only plan.</summary>
    public decimal DiscountPercent { get; private set; }

    /// <summary>Display-only today - no slot-priority queue exists yet to wire this into; carried so the plan's full benefit set (PRODUCT-ENHANCEMENTS.md #1) is captured now rather than needing a schema change later.</summary>
    public bool PrioritySlotFlag { get; private set; }

    public bool IsActive { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public Guid? UpdatedByAdminUserId { get; private set; }

    protected SubscriptionPlan() { }

    public SubscriptionPlan(
        Guid id,
        string name,
        string? description,
        decimal price,
        SubscriptionBillingCycle billingCycle,
        int freeVisitsIncluded,
        decimal discountPercent,
        bool prioritySlotFlag)
        : base(id)
    {
        ValidateFields(name, price, freeVisitsIncluded, discountPercent);

        Name = name.Trim();
        Description = description;
        Price = price;
        BillingCycle = billingCycle;
        FreeVisitsIncluded = freeVisitsIncluded;
        DiscountPercent = discountPercent;
        PrioritySlotFlag = prioritySlotFlag;
        IsActive = true;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
    }

    /// <summary>Edits every mutable field (task 180's admin CRUD). Same validation as the constructor - an edit must leave the plan in a state the constructor itself would have accepted. Existing subscribers are unaffected until their next renewal snapshots the new terms (see <see cref="CustomerSubscription.RecordSuccessfulRenewal"/>) - a live subscriber's current period never changes underneath them mid-cycle.</summary>
    public void Update(
        string name,
        string? description,
        decimal price,
        SubscriptionBillingCycle billingCycle,
        int freeVisitsIncluded,
        decimal discountPercent,
        bool prioritySlotFlag,
        Guid? updatedByAdminUserId)
    {
        ValidateFields(name, price, freeVisitsIncluded, discountPercent);

        Name = name.Trim();
        Description = description;
        Price = price;
        BillingCycle = billingCycle;
        FreeVisitsIncluded = freeVisitsIncluded;
        DiscountPercent = discountPercent;
        PrioritySlotFlag = prioritySlotFlag;
        UpdatedAtUtc = DateTime.UtcNow;
        UpdatedByAdminUserId = updatedByAdminUserId;
    }

    /// <summary>Re-enables a plan for new subscriptions - existing subscribers were never affected by deactivation in the first place (see <see cref="Deactivate"/>).</summary>
    public void Activate(Guid? updatedByAdminUserId)
    {
        IsActive = true;
        UpdatedAtUtc = DateTime.UtcNow;
        UpdatedByAdminUserId = updatedByAdminUserId;
    }

    /// <summary>Suspends a plan without deleting it - mirrors <see cref="Coupon.Deactivate"/>: no new subscriptions can start on it, but every existing <see cref="CustomerSubscription"/> already on this plan (its terms already snapshotted) keeps renewing normally.</summary>
    public void Deactivate(Guid? updatedByAdminUserId)
    {
        IsActive = false;
        UpdatedAtUtc = DateTime.UtcNow;
        UpdatedByAdminUserId = updatedByAdminUserId;
    }

    private static void ValidateFields(string name, decimal price, int freeVisitsIncluded, decimal discountPercent)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Plan name is required.", nameof(name));
        }

        if (price < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(price), "Plan price cannot be negative.");
        }

        if (freeVisitsIncluded < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(freeVisitsIncluded), "Free visits included cannot be negative.");
        }

        if (discountPercent < 0 || discountPercent > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(discountPercent), "Discount percent must be between 0 and 100.");
        }
    }
}

using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>
/// An add-on selected against a <see cref="BookingItem"/>, snapshotted at
/// booking time (SRS 23.3, task 59d) - name and price are copied from
/// <see cref="ServiceAddOn"/> as of the moment of booking so a later catalog
/// price change can never rewrite a historical booking's total.
/// </summary>
public class BookingAddOnItem : Entity<Guid>
{
    public Guid BookingItemId { get; private set; }

    /// <summary>Traceability only - not a foreign key. Deleting/deactivating the source add-on must never affect this row.</summary>
    public Guid ServiceAddOnId { get; private set; }

    public string NameSnapshot { get; private set; } = string.Empty;
    public decimal UnitPriceSnapshot { get; private set; }
    public int Quantity { get; private set; }
    public decimal LineTotalSnapshot { get; private set; }

    /// <summary>Traceability only - not a foreign key, same convention as <see cref="ServiceAddOnId"/>. Null when the add-on was ungrouped (today's default) at booking time.</summary>
    public Guid? AddOnGroupId { get; private set; }
    public string? GroupNameSnapshot { get; private set; }

    protected BookingAddOnItem() { }

    public BookingAddOnItem(Guid id, Guid bookingItemId, Guid serviceAddOnId, string nameSnapshot, decimal unitPriceSnapshot, int quantity)
        : base(id)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
        }

        BookingItemId = bookingItemId;
        ServiceAddOnId = serviceAddOnId;
        NameSnapshot = nameSnapshot ?? throw new ArgumentNullException(nameof(nameSnapshot));
        UnitPriceSnapshot = unitPriceSnapshot;
        Quantity = quantity;
        LineTotalSnapshot = unitPriceSnapshot * quantity;
    }

    /// <summary>Same as the base constructor, plus the add-on-group snapshot fields (Phase 3 catalog redesign) - used when the selected add-on belonged to a <see cref="ServiceAddOnGroup"/>.</summary>
    public BookingAddOnItem(
        Guid id, Guid bookingItemId, Guid serviceAddOnId, string nameSnapshot, decimal unitPriceSnapshot, int quantity,
        Guid? addOnGroupId, string? groupNameSnapshot)
        : this(id, bookingItemId, serviceAddOnId, nameSnapshot, unitPriceSnapshot, quantity)
    {
        AddOnGroupId = addOnGroupId;
        GroupNameSnapshot = groupNameSnapshot;
    }
}

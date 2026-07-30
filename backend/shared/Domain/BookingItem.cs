using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>
/// A service booked within a <see cref="Booking"/>, snapshotted at booking
/// time (SRS 23.3, task 59c) - name and price are copied from
/// <see cref="Service"/> as of the moment of booking. The cart model is
/// single-service for now (SRS 11.7.1, task 57), so a booking has exactly
/// one of these, but the schema is shaped to carry more than one without a
/// migration when that changes.
/// </summary>
public class BookingItem : Entity<Guid>
{
    private readonly List<BookingAddOnItem> _addOns = [];

    public Guid BookingId { get; private set; }

    /// <summary>Traceability only - not a foreign key. Deleting/deactivating the source service must never affect this row.</summary>
    public Guid ServiceId { get; private set; }

    public string NameSnapshot { get; private set; } = string.Empty;
    public string SlugSnapshot { get; private set; } = string.Empty;
    public decimal UnitPriceSnapshot { get; private set; }
    public int Quantity { get; private set; }
    public decimal LineTotalSnapshot { get; private set; }

    public IReadOnlyList<BookingAddOnItem> AddOns => _addOns;

    protected BookingItem() { }

    public BookingItem(Guid id, Guid bookingId, Guid serviceId, string nameSnapshot, string slugSnapshot, decimal unitPriceSnapshot, int quantity)
        : base(id)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
        }

        BookingId = bookingId;
        ServiceId = serviceId;
        NameSnapshot = nameSnapshot ?? throw new ArgumentNullException(nameof(nameSnapshot));
        SlugSnapshot = slugSnapshot ?? throw new ArgumentNullException(nameof(slugSnapshot));
        UnitPriceSnapshot = unitPriceSnapshot;
        Quantity = quantity;
        LineTotalSnapshot = unitPriceSnapshot * quantity;
    }

    public BookingAddOnItem AddAddOn(Guid id, Guid serviceAddOnId, string nameSnapshot, decimal unitPriceSnapshot, int quantity)
    {
        var addOn = new BookingAddOnItem(id, Id, serviceAddOnId, nameSnapshot, unitPriceSnapshot, quantity);
        _addOns.Add(addOn);
        return addOn;
    }
}

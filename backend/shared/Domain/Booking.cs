using Nestly.BuildingBlocks.Primitives;
using Nestly.Domain.Events;

namespace Nestly.Domain;

/// <summary>
/// A customer's booking (SRS 13, 23.3). The aggregate root for the booking
/// domain - owns its line items, their add-ons, and its status history as a
/// single consistency boundary, since none of those have any meaning or
/// lifecycle outside their parent booking.
///
/// Every customer/address/slot/price field here is a snapshot taken at
/// booking time, not a live join - SRS 14.1 ("booking stores full price
/// snapshot") and the note on <see cref="CustomerAddress"/> both require a
/// booking to keep reading the same values forever, even after the source
/// catalog price, address, or profile changes or is deleted.
/// </summary>
public class Booking : AggregateRoot<Guid>
{
    private readonly List<BookingItem> _items = [];
    private readonly List<BookingStatusHistory> _statusHistory = [];

    public Guid CustomerId { get; private set; }
    public string CustomerNameSnapshot { get; private set; } = string.Empty;
    public string CustomerMobileSnapshot { get; private set; } = string.Empty;

    /// <summary>Traceability only - not a foreign key. See <see cref="CustomerAddress"/>'s doc comment.</summary>
    public Guid? SourceAddressId { get; private set; }

    public string AddressLabelSnapshot { get; private set; } = string.Empty;
    public string AddressLine1Snapshot { get; private set; } = string.Empty;
    public string? AddressLine2Snapshot { get; private set; }
    public string? AddressLandmarkSnapshot { get; private set; }
    public string AddressPincodeSnapshot { get; private set; } = string.Empty;
    public string AddressCitySnapshot { get; private set; } = string.Empty;
    public string AddressStateSnapshot { get; private set; } = string.Empty;
    public decimal AddressLatitudeSnapshot { get; private set; }
    public decimal AddressLongitudeSnapshot { get; private set; }
    public string AddressContactNameSnapshot { get; private set; } = string.Empty;
    public string AddressContactMobileSnapshot { get; private set; } = string.Empty;

    /// <summary>Traceability only - not a foreign key. The window's own config can change or be removed without affecting a placed booking.</summary>
    public Guid SlotWindowId { get; private set; }

    public DateOnly SlotDate { get; private set; }
    public string SlotWindowNameSnapshot { get; private set; } = string.Empty;
    public TimeSpan SlotStartTimeSnapshot { get; private set; }
    public TimeSpan SlotEndTimeSnapshot { get; private set; }

    public decimal BasePriceSnapshot { get; private set; }
    public int QuantitySnapshot { get; private set; }
    public decimal BaseTotalSnapshot { get; private set; }
    public decimal AddOnTotalSnapshot { get; private set; }
    public decimal VisitChargeSnapshot { get; private set; }
    public decimal SubtotalSnapshot { get; private set; }
    public decimal TaxPercentageSnapshot { get; private set; }
    public decimal TaxAmountSnapshot { get; private set; }
    public decimal PlatformFeeSnapshot { get; private set; }
    public decimal TotalPayableSnapshot { get; private set; }

    /// <summary>Null until Phase 4's coupon module exists - no coupon domain to validate against yet.</summary>
    public string? CouponCodeSnapshot { get; private set; }
    public decimal? CouponDiscountAmountSnapshot { get; private set; }

    public BookingStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public IReadOnlyList<BookingItem> Items => _items;
    public IReadOnlyList<BookingStatusHistory> StatusHistory => _statusHistory;

    protected Booking() { }

    public Booking(
        Guid id,
        Guid customerId,
        CustomerSnapshot customer,
        Guid? sourceAddressId,
        AddressSnapshot address,
        SlotSnapshot slot,
        PriceSnapshot price,
        string? couponCode = null,
        decimal? couponDiscountAmount = null)
        : base(id)
    {
        ArgumentNullException.ThrowIfNull(customer);
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(slot);
        ArgumentNullException.ThrowIfNull(price);

        CustomerId = customerId;
        CustomerNameSnapshot = customer.Name ?? throw new ArgumentException("Customer name is required.", nameof(customer));
        CustomerMobileSnapshot = customer.Mobile ?? throw new ArgumentException("Customer mobile is required.", nameof(customer));

        SourceAddressId = sourceAddressId;
        AddressLabelSnapshot = address.Label ?? string.Empty;
        AddressLine1Snapshot = address.Line1 ?? throw new ArgumentException("Address line 1 is required.", nameof(address));
        AddressLine2Snapshot = address.Line2;
        AddressLandmarkSnapshot = address.Landmark;
        AddressPincodeSnapshot = address.Pincode ?? throw new ArgumentException("Address pincode is required.", nameof(address));
        AddressCitySnapshot = address.City ?? throw new ArgumentException("Address city is required.", nameof(address));
        AddressStateSnapshot = address.State ?? throw new ArgumentException("Address state is required.", nameof(address));
        AddressLatitudeSnapshot = address.Latitude;
        AddressLongitudeSnapshot = address.Longitude;
        AddressContactNameSnapshot = address.ContactName ?? throw new ArgumentException("Address contact name is required.", nameof(address));
        AddressContactMobileSnapshot = address.ContactMobile ?? throw new ArgumentException("Address contact mobile is required.", nameof(address));

        SlotWindowId = slot.SlotWindowId;
        SlotDate = slot.Date;
        SlotWindowNameSnapshot = slot.WindowName ?? string.Empty;
        SlotStartTimeSnapshot = slot.StartTime;
        SlotEndTimeSnapshot = slot.EndTime;

        BasePriceSnapshot = price.BasePrice;
        QuantitySnapshot = price.Quantity > 0 ? price.Quantity : throw new ArgumentOutOfRangeException(nameof(price), "Quantity must be positive.");
        BaseTotalSnapshot = price.BaseTotal;
        AddOnTotalSnapshot = price.AddOnTotal;
        VisitChargeSnapshot = price.VisitCharge;
        SubtotalSnapshot = price.Subtotal;
        TaxPercentageSnapshot = price.TaxPercentage;
        TaxAmountSnapshot = price.TaxAmount;
        PlatformFeeSnapshot = price.PlatformFee;
        TotalPayableSnapshot = price.TotalPayable;

        CouponCodeSnapshot = couponCode;
        CouponDiscountAmountSnapshot = couponDiscountAmount;

        Status = BookingStatus.Initiated;
        CreatedAtUtc = DateTime.UtcNow;
        RecordStatusHistory(null, BookingStatus.Initiated, reason: null);
        RaiseDomainEvent(new BookingCreatedEvent(Id, CustomerId));
    }

    /// <summary>
    /// Adds a booked service line (task 57's single-service cart today; the
    /// schema allows more). Only while the booking is still <see cref="BookingStatus.Initiated"/> -
    /// SRS 14.1's price snapshot must stop moving the instant payment starts.
    /// </summary>
    public BookingItem AddItem(Guid id, Guid serviceId, string nameSnapshot, string slugSnapshot, decimal unitPriceSnapshot, int quantity)
    {
        EnsureStillMutable();

        var item = new BookingItem(id, Id, serviceId, nameSnapshot, slugSnapshot, unitPriceSnapshot, quantity);
        _items.Add(item);
        return item;
    }

    /// <summary>
    /// Advances the booking to <paramref name="newStatus"/> if
    /// <see cref="BookingLifecycle"/> allows it from the current status, and
    /// appends a status history row - the only way either ever changes (SRS
    /// 13.2: "invalid transitions must be blocked").
    /// </summary>
    public void TransitionTo(BookingStatus newStatus, string? reason = null)
    {
        if (!BookingLifecycle.IsValidTransition(Status, newStatus))
        {
            throw new InvalidOperationException($"Cannot transition a booking from {Status} to {newStatus}.");
        }

        var previousStatus = Status;
        Status = newStatus;
        RecordStatusHistory(previousStatus, newStatus, reason);
        RaiseDomainEvent(new BookingStatusChangedEvent(Id, previousStatus, newStatus));
    }

    private void EnsureStillMutable()
    {
        if (Status != BookingStatus.Initiated)
        {
            throw new InvalidOperationException(
                $"Booking items can only be added while the booking is Initiated (current status: {Status}).");
        }
    }

    private void RecordStatusHistory(BookingStatus? from, BookingStatus to, string? reason) =>
        _statusHistory.Add(new BookingStatusHistory(Guid.NewGuid(), Id, from, to, reason, DateTime.UtcNow));
}

using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>
/// A customer's saved address (SRS 11.3.2). Exactly one address per customer
/// may have <see cref="IsDefault"/> set; <see cref="CustomerAddressService"/>
/// owns that invariant, not this entity.
///
/// SRS 11.3.3: deleting an address must not affect bookings that already
/// captured it. This entity supports a real delete for that reason — a
/// booking must copy the address fields it needs onto the booking itself at
/// booking time rather than holding a foreign key to this table, so a later
/// delete here has nothing to cascade into. That denormalization is the
/// booking feature's responsibility (not yet built); it is not solved here.
/// </summary>
public class CustomerAddress : Entity<Guid>
{
    public Guid CustomerId { get; private set; }
    public string Label { get; private set; } = string.Empty;
    public string Line1 { get; private set; } = string.Empty;
    public string? Line2 { get; private set; }
    public string? Landmark { get; private set; }
    public string Pincode { get; private set; } = string.Empty;
    public string City { get; private set; } = string.Empty;
    public string State { get; private set; } = string.Empty;
    public decimal Latitude { get; private set; }
    public decimal Longitude { get; private set; }
    public string ContactName { get; private set; } = string.Empty;
    public string ContactMobile { get; private set; } = string.Empty;
    public bool IsDefault { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    /// <summary>
    /// Best-effort link into the Geography module (SRS 12.9.1), resolved from
    /// <see cref="Pincode"/>'s free-text code by <c>CustomerAddressService</c>
    /// after every create/update. Null when no active pincode matches -
    /// booking/serviceability checks that need this must handle that case
    /// rather than assume every address resolves.
    /// </summary>
    public Guid? PincodeId { get; private set; }

    /// <summary>
    /// Null for now: this entity has no locality-name field to match against
    /// <see cref="Locality"/>, so there is nothing reliable to resolve this
    /// from yet. Wire it up if/when the address form gains a locality picker.
    /// </summary>
    public Guid? LocalityId { get; private set; }

    protected CustomerAddress() { }

    public CustomerAddress(
        Guid id,
        Guid customerId,
        string label,
        string line1,
        string? line2,
        string? landmark,
        string pincode,
        string city,
        string state,
        decimal latitude,
        decimal longitude,
        string contactName,
        string contactMobile,
        bool isDefault) : base(id)
    {
        CustomerId = customerId;
        Label = label ?? throw new ArgumentNullException(nameof(label));
        Line1 = line1 ?? throw new ArgumentNullException(nameof(line1));
        Line2 = line2;
        Landmark = landmark;
        Pincode = pincode ?? throw new ArgumentNullException(nameof(pincode));
        City = city ?? throw new ArgumentNullException(nameof(city));
        State = state ?? throw new ArgumentNullException(nameof(state));
        Latitude = latitude;
        Longitude = longitude;
        ContactName = contactName ?? throw new ArgumentNullException(nameof(contactName));
        ContactMobile = contactMobile ?? throw new ArgumentNullException(nameof(contactMobile));
        IsDefault = isDefault;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Update(
        string label,
        string line1,
        string? line2,
        string? landmark,
        string pincode,
        string city,
        string state,
        decimal latitude,
        decimal longitude,
        string contactName,
        string contactMobile)
    {
        Label = label ?? throw new ArgumentNullException(nameof(label));
        Line1 = line1 ?? throw new ArgumentNullException(nameof(line1));
        Line2 = line2;
        Landmark = landmark;
        Pincode = pincode ?? throw new ArgumentNullException(nameof(pincode));
        City = city ?? throw new ArgumentNullException(nameof(city));
        State = state ?? throw new ArgumentNullException(nameof(state));
        Latitude = latitude;
        Longitude = longitude;
        ContactName = contactName ?? throw new ArgumentNullException(nameof(contactName));
        ContactMobile = contactMobile ?? throw new ArgumentNullException(nameof(contactMobile));
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Called by the service layer after resolving <see cref="Pincode"/> against the geography master.</summary>
    public void LinkToGeography(Guid? pincodeId, Guid? localityId)
    {
        PincodeId = pincodeId;
        LocalityId = localityId;
    }

    public void MarkAsDefault() => IsDefault = true;

    public void ClearDefault() => IsDefault = false;
}

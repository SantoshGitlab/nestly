namespace Nestly.Domain;

/// <summary>
/// Parameter objects for <see cref="Booking"/>'s constructor (SRS 23.3, task
/// 59) - plain data carried in from the Application layer at booking time and
/// copied field-by-field onto the booking. Not persisted as their own EF
/// entities; the values live as columns on <see cref="Booking"/>.
/// </summary>
public sealed record CustomerSnapshot(string Name, string Mobile);

/// <summary>
/// Mirrors <see cref="CustomerAddress"/>'s fields at the moment of booking.
/// Deliberately not a foreign key to that table - see the comment on
/// <see cref="CustomerAddress"/> on why a booking must own a copy rather than
/// a reference.
/// </summary>
public sealed record AddressSnapshot(
    string Label,
    string Line1,
    string? Line2,
    string? Landmark,
    string Pincode,
    string City,
    string State,
    decimal Latitude,
    decimal Longitude,
    string ContactName,
    string ContactMobile);

public sealed record SlotSnapshot(Guid SlotWindowId, DateOnly Date, string WindowName, TimeSpan StartTime, TimeSpan EndTime);

/// <summary>Mirrors <c>PriceBreakdownResponse</c> at the moment of booking (SRS 14.1 - "booking stores full price snapshot").</summary>
public sealed record PriceSnapshot(
    decimal BasePrice,
    int Quantity,
    decimal BaseTotal,
    decimal AddOnTotal,
    decimal VisitCharge,
    decimal Subtotal,
    decimal TaxPercentage,
    decimal TaxAmount,
    decimal PlatformFee,
    decimal TotalPayable);

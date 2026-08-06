using System.ComponentModel.DataAnnotations;

namespace Nestly.Infrastructure.Options;

/// <summary>
/// Strongly typed binding of the "Booking" configuration section: platform
/// limits applied to every booking request regardless of service.
/// </summary>
public class BookingOptions
{
    public const string SectionName = "Booking";

    /// <summary>
    /// Largest quantity a single booking may be placed for.
    ///
    /// A booking occupies one slot on one address, so quantity is "how many
    /// units of this service at this visit", not a shopping cart - and one
    /// booking consumes exactly one seat of the slot's capacity no matter
    /// what the quantity is. Without a ceiling, holding down the "+" control
    /// produced a single ₹74,950 booking for fifty deep cleans in one
    /// four-hour morning window, which no city can staff.
    ///
    /// Platform-wide rather than per-service because no per-service limit
    /// column exists yet; add one to <c>Service</c> and treat this as the
    /// fallback if services ever need to differ.
    /// </summary>
    [Range(1, 100)]
    public int MaxQuantityPerBooking { get; set; } = 10;
}

using System.ComponentModel.DataAnnotations;

namespace Nestly.Infrastructure.Options;

/// <summary>Strongly typed binding of the "RecurringBookings" configuration section (task 185).</summary>
public class RecurringBookingOptions
{
    public const string SectionName = "RecurringBookings";

    /// <summary>
    /// How many days ahead of a scheduled occurrence the job attempts it.
    /// PRODUCT-ENHANCEMENTS.md section 2 requires "enough lead time to catch
    /// and surface a problem before the customer expects the visit" without
    /// naming a number - 3 days is chosen so a skip-and-notify reaches the
    /// customer with real time to react (rebook manually, or simply expect
    /// the plan to continue on its next date), while staying close enough to
    /// the visit that the address/catalog state the orchestration validates
    /// is still representative of what the customer will actually get.
    /// </summary>
    [Range(1, 30)]
    public int LeadTimeDays { get; set; } = 3;
}

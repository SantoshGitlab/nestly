using System.ComponentModel.DataAnnotations;

namespace Nestly.Infrastructure.Options;

/// <summary>
/// Strongly typed binding of the "BookingExpiry" configuration section (task
/// 240/242). No admin UI to manage this yet, same not-yet-adminable-policy-knob
/// reasoning as <see cref="CommissionOptions"/>.
/// </summary>
public class BookingExpiryOptions
{
    public const string SectionName = "BookingExpiry";

    /// <summary>
    /// How long a booking may sit in PaymentPending, holding its slot seat,
    /// before BookingExpirySweepJob expires it. Task 242's decision: 20
    /// minutes - long enough to cover a slow payment-gateway redirect or a
    /// customer briefly switching apps mid-payment, short enough that an
    /// abandoned seat is back in the pool the same booking session, not
    /// held for hours against real demand.
    /// </summary>
    [Range(1, 1440)]
    public int ExpiryMinutes { get; set; } = 20;
}

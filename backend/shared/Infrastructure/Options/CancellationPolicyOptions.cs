using System.ComponentModel.DataAnnotations;

namespace Nestly.Infrastructure.Options;

/// <summary>
/// Strongly typed binding of the "CancellationPolicy" configuration section
/// (SRS 11.14.1, task 80a-b). There is no admin auth/UI to manage
/// cancellation policy yet (that's Phase 6) - the same not-yet-adminable
/// policy-knob approach this codebase already uses for commission rates
/// (see <see cref="CommissionOptions"/>) applies here too.
/// </summary>
public class CancellationPolicyOptions
{
    public const string SectionName = "CancellationPolicy";

    /// <summary>
    /// Cancelling at least this many hours before the booked slot owes no
    /// cancellation fee - a full refund of whatever was paid.
    /// </summary>
    [Range(0, 720)]
    public decimal FreeCancellationWindowHours { get; set; } = 4m;

    /// <summary>
    /// Percentage of the payable amount retained as a cancellation fee when
    /// cancelling inside the free window (0-100).
    /// </summary>
    [Range(0, 100)]
    public decimal LateCancellationFeePercentage { get; set; } = 20m;
}

using System.ComponentModel.DataAnnotations;

namespace Nestly.Infrastructure.Options;

/// <summary>
/// Strongly typed binding of the "ReschedulePolicy" configuration section
/// (SRS 11.15.1, task 82a-d). Same not-yet-adminable policy-knob approach as
/// <see cref="CancellationPolicyOptions"/> - no admin auth/UI to manage this
/// yet (Phase 6).
/// </summary>
public class ReschedulePolicyOptions
{
    public const string SectionName = "ReschedulePolicy";

    /// <summary>Rescheduling with less than this many hours to the current slot is blocked entirely (SRS 11.15.1 "window has not expired").</summary>
    [Range(0, 720)]
    public decimal MinHoursBeforeSlot { get; set; } = 2m;

    /// <summary>How many times a single booking may be rescheduled before further reschedules are blocked (SRS 11.15.1 "count limit").</summary>
    [Range(0, 50)]
    public int MaxReschedulesPerBooking { get; set; } = 2;

    /// <summary>Rescheduling with less than this many hours to go (but still above <see cref="MinHoursBeforeSlot"/>) incurs a fee.</summary>
    [Range(0, 720)]
    public decimal LateFeeThresholdHours { get; set; } = 6m;

    /// <summary>Percentage of the booking's payable amount reported as a late-reschedule fee (0-100).</summary>
    [Range(0, 100)]
    public decimal LateRescheduleFeePercentage { get; set; } = 10m;
}

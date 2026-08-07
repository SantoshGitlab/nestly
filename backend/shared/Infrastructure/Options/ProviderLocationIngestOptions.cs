using System.ComponentModel.DataAnnotations;

namespace Nestly.Infrastructure.Options;

/// <summary>
/// Strongly typed binding of the "ProviderLocationIngest" configuration
/// section (task 269) - the acceptance policy for
/// <c>POST /api/v1/jobs/{bookingId}/location</c>. Not a secret and every
/// value has a safe production-sensible default, same
/// not-yet-adminable-policy-knob reasoning as <see cref="AutoAssignmentOptions"/>.
/// </summary>
/// <remarks>
/// These are the three numbers that decide how much location data the
/// platform collects and how much of it it trusts. None of them has
/// production data behind it yet, which is exactly why they are configuration
/// rather than constants.
/// </remarks>
public class ProviderLocationIngestOptions
{
    public const string SectionName = "ProviderLocationIngest";

    /// <summary>
    /// The minimum gap between two accepted fixes for the same booking. A
    /// ping arriving sooner is dropped with 202 rather than refused, so a
    /// chatty or looping client degrades to this rate instead of writing
    /// thousands of rows.
    /// </summary>
    /// <remarks>
    /// Fifteen seconds is roughly the resolution a moving-vehicle marker
    /// needs to look live without jumping, and caps one job at ~240 rows an
    /// hour. Setting it to 0 disables throttling entirely - legitimate for a
    /// load test, never for production.
    /// </remarks>
    [Range(0, 3600)]
    public int MinimumIntervalSeconds { get; set; } = 15;

    /// <summary>
    /// How far in the past a device's <c>recordedAt</c> may be and still be
    /// accepted. A fix older than this is refused rather than stored,
    /// because the tracking screen would render it as the provider's current
    /// position when it is not.
    /// </summary>
    /// <remarks>
    /// This is the window a queued-up offline client can replay through, so
    /// it also bounds the throttle's burst allowance: at most
    /// <see cref="MaximumAgeMinutes"/> / <see cref="MinimumIntervalSeconds"/>
    /// rows (5 min / 15 s = 20 by default) can be written back-to-back before
    /// the client has to wait for real time to advance again.
    /// </remarks>
    [Range(1, 60)]
    public int MaximumAgeMinutes { get; set; } = 5;

    /// <summary>
    /// How far a device clock may run ahead of the server before its fixes
    /// are refused as future-dated.
    /// </summary>
    /// <remarks>
    /// The rule is "not in the future", but enforcing that to the millisecond
    /// would reject a perfectly healthy phone whose clock is two seconds
    /// fast - i.e. it would break the feature for real devices rather than
    /// catch bad ones. Thirty seconds is small enough that it cannot be used
    /// to fabricate a plausible-looking future trail. Set it to 0 for the
    /// strict reading.
    /// </remarks>
    [Range(0, 300)]
    public int FutureSkewToleranceSeconds { get; set; } = 30;
}

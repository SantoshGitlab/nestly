using System.ComponentModel.DataAnnotations;

namespace Nestly.Infrastructure.Options;

/// <summary>
/// Strongly typed binding of the "BookingEta" configuration section (task
/// 271) - how often the platform is willing to pay for a route lookup while a
/// job is being tracked. Not a secret, and every value has a safe
/// production-sensible default, same not-yet-adminable-policy-knob reasoning
/// as <see cref="ProviderLocationIngestOptions"/>.
/// </summary>
/// <remarks>
/// <b>This is a billing control, not a tuning preference.</b> Route lookups
/// cost real money per element, and location pings arrive as fast as a moving
/// phone can send them. <see cref="ProviderLocationIngestOptions.MinimumIntervalSeconds"/>
/// caps how many pings are *stored*; these two cap how many of those pings
/// turn into a paid request, which is a different question and deliberately a
/// different pair of knobs. Widening either of them multiplies the maps bill
/// directly.
/// </remarks>
public class BookingEtaOptions
{
    public const string SectionName = "BookingEta";

    /// <summary>
    /// The minimum age a stored ETA must reach before elapsed time alone
    /// justifies recomputing it.
    /// </summary>
    /// <remarks>
    /// Sixty seconds: the tracking screen renders whole minutes, so an
    /// estimate refreshed more often than once a minute cannot tell the
    /// customer anything new by age alone. Against the default 15-second
    /// ingest throttle this already cuts the worst case from 240 lookups an
    /// hour per job to 60, before
    /// <see cref="MinimumMovementMetres"/> takes anything off. Setting it to 0
    /// makes every accepted ping pay for a lookup - legitimate for a demo,
    /// never for production.
    /// </remarks>
    [Range(0, 3600)]
    public int MinimumRecomputeIntervalSeconds { get; set; } = 60;

    /// <summary>
    /// How far the provider must have moved from the position the current
    /// estimate was computed at before movement alone justifies recomputing
    /// it, in metres.
    /// </summary>
    /// <remarks>
    /// This is the gate that keeps the ETA honest between time windows: a
    /// provider covering ground fast invalidates the stored number long before
    /// it is a minute old. Two hundred and fifty metres is roughly a city
    /// block, far enough that GPS scatter while parked (tens of metres, and
    /// worse between buildings) cannot trip it, close enough that a vehicle in
    /// traffic crosses it in well under a minute. Setting it to 0 would make
    /// every jitter-sized fix pay for a lookup, so the range starts at 1.
    /// </remarks>
    [Range(1, 100_000)]
    public decimal MinimumMovementMetres { get; set; } = 250m;
}

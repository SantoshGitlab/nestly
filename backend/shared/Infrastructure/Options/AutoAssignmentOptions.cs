using System.ComponentModel.DataAnnotations;

namespace Nestly.Infrastructure.Options;

/// <summary>
/// Strongly typed binding of the "AutoAssignment" configuration section
/// (PROVIDER.md OPEN DECISIONS - AUTOMATIC ASSIGNMENT, tasks 247/248). No
/// admin UI to manage this yet, same not-yet-adminable-policy-knob reasoning
/// as <see cref="CommissionOptions"/>.
/// </summary>
public class AutoAssignmentOptions
{
    public const string SectionName = "AutoAssignment";

    /// <summary>
    /// Task 248's kill switch: when false, <c>ProviderAutoAssignmentHandler</c>
    /// takes no action at all on any booking - falls back to today's fully
    /// manual admin-assignment flow, with no other behaviour change. Default
    /// true (same as <see cref="Nestly.Infrastructure.Options.BackgroundJobOptions.ServerEnabled"/>'s
    /// convention: on by default, an explicit override to turn off) - lets
    /// ops disable a misbehaving first-release auto-dispatch engine in
    /// production without a deploy, purely via configuration.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Task 247: how many times <c>ProviderAutoAssignmentHandler</c> retries
    /// the next-best candidate after a rejection before leaving the booking
    /// for the manual admin queue. Decision 6: 3 - past a handful of
    /// declines the pattern is more likely a genuinely hard-to-place booking
    /// than one more retry fixing it, and the number itself has no
    /// production data behind it yet, hence configurable rather than a
    /// hardcoded constant.
    /// </summary>
    [Range(0, 20)]
    public int RetryAttempts { get; set; } = 3;

    /// <summary>
    /// Task 267's kill switch: when false, <c>ProviderMatchingService</c>
    /// ranks candidates purely by great-circle distance, exactly as it did
    /// before real travel time became an input - no route call is issued and
    /// <c>ProviderMatchCandidate.TravelDurationSeconds</c> is null on every
    /// candidate. Default true, same convention as <see cref="Enabled"/> and
    /// <see cref="GoogleMapsOptions.Enabled"/>: on by default, an explicit
    /// override to turn off, so a routing outage or a billing incident is one
    /// configuration change rather than a deploy.
    /// </summary>
    /// <remarks>
    /// Turning it off is not the only way to stop paying for routing:
    /// <see cref="GoogleMapsOptions.Enabled"/> already forces the sandbox
    /// estimator, and a sandbox-only response is deliberately ignored for
    /// ranking (see <c>ProviderMatchingService</c>). This switch exists as
    /// well because it also removes the call itself - the cheapest possible
    /// posture - and because a kill switch for a feature should live beside
    /// the feature's own options.
    /// </remarks>
    public bool RouteRankingEnabled { get; set; } = true;

    /// <summary>
    /// How many of the straight-line-nearest candidates are priced with a real
    /// route call. This is the cost cap: candidate discovery filters by skill
    /// and service area, but a popular city can still leave dozens of eligible
    /// providers, and every extra destination is a billed Routes API element.
    /// Ten keeps one booking to a single request (well under
    /// <see cref="GoogleMapsOptions.MaxDestinationsPerCall"/>) while still
    /// giving road travel time enough candidates to reorder - past the ten
    /// nearest by air, a candidate winning on road time is vanishingly
    /// unlikely.
    /// </summary>
    /// <remarks>
    /// A cap, never a filter: candidates beyond it are still returned and
    /// still assignable, just ordered by great-circle distance behind the
    /// priced ones (which are, by construction, all nearer by air).
    /// </remarks>
    [Range(1, 50)]
    public int MaxRouteCandidates { get; set; } = 10;

    /// <summary>
    /// Great-circle radius, in kilometres, beyond which a candidate is not
    /// worth a route call. Twenty-five covers a metro's realistic service
    /// radius; a provider farther away than that loses on travel time however
    /// the road runs, so paying to measure it exactly buys nothing.
    /// </summary>
    /// <remarks>
    /// Also a cost cap, not an eligibility rule: a candidate outside the
    /// radius is still returned and still assignable - if it is the only
    /// candidate it is still the one that gets assigned, with no route call
    /// issued at all. Excluding it here would silently narrow the eligible
    /// pool that skill/service-area/availability already define, which is a
    /// different (product) decision than "don't spend money measuring it".
    /// </remarks>
    [Range(0.1, 1000.0)]
    public decimal RouteRankingRadiusKm { get; set; } = 25m;
}

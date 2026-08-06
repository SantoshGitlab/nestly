using System.ComponentModel.DataAnnotations;

namespace Nestly.Infrastructure.Options;

/// <summary>
/// Strongly typed binding of the "RouteEstimates:Sandbox" configuration
/// section (task 266) - the two constants the sandbox estimator turns a
/// straight-line distance into a road journey with. Same
/// abstraction-then-colon-Sandbox shape as
/// <see cref="SandboxGatewayOptions"/>'s "PaymentGateway:Sandbox".
/// </summary>
/// <remarks>
/// Neither value is a secret and both have defaults that produce sensible
/// numbers, so a deployment that sets nothing here still gets a working -
/// merely approximate - ETA.
/// </remarks>
public class SandboxRouteEstimateOptions
{
    public const string SectionName = "RouteEstimates:Sandbox";

    /// <summary>
    /// How much longer the road is than the straight line between two points
    /// (the "detour index"). 1.3 is the usual figure for a dense urban street
    /// grid - the road distance between two points a kilometre apart as the
    /// crow flies typically comes out near 1.3 km.
    /// </summary>
    [Range(1.0, 5.0)]
    public decimal RoadWindingFactor { get; set; } = 1.3m;

    /// <summary>
    /// Average door-to-door driving speed, km/h. 25 reflects Indian metro
    /// traffic (this platform is Asia/Kolkata, pincode-addressed), where a
    /// free-flow speed limit is a poor predictor of arrival time.
    /// </summary>
    [Range(1.0, 200.0)]
    public decimal AverageSpeedKph { get; set; } = 25m;
}

namespace Nestly.BuildingBlocks.Geo;

/// <summary>
/// Great-circle (Haversine) distance between two WGS84 coordinate pairs.
/// PROVIDER.md OPEN DECISIONS - AUTOMATIC ASSIGNMENT #1: there is no PostGIS
/// in this solution, so proximity is plain <see cref="Math"/> over the
/// decimal(9,6) latitude/longitude columns.
///
/// Coordinates are optional everywhere in this system - a provider may never
/// have reported a location, a booking address may not have geocoded - so a
/// distance only exists when all four components are present. Callers get
/// <c>null</c> rather than a sentinel they might accidentally rank or
/// threshold on.
/// </summary>
public static class GeoDistance
{
    private const double EarthRadiusKilometres = 6371.0;
    private const double EarthRadiusMetres = 6_371_000.0;

    /// <summary>
    /// Distance in kilometres, or <c>null</c> when either point is missing a
    /// latitude or a longitude.
    /// </summary>
    public static decimal? KilometresBetween(decimal? latitude1, decimal? longitude1, decimal? latitude2, decimal? longitude2)
    {
        double? centralAngle = CentralAngleRadians(latitude1, longitude1, latitude2, longitude2);
        return centralAngle is null ? null : (decimal)(EarthRadiusKilometres * centralAngle.Value);
    }

    /// <summary>
    /// Distance in metres, or <c>null</c> when either point is missing a
    /// latitude or a longitude. For movement thresholds ("has this provider
    /// moved more than N metres?"), which kilometres would force into awkward
    /// fractions.
    /// </summary>
    public static decimal? MetresBetween(decimal? latitude1, decimal? longitude1, decimal? latitude2, decimal? longitude2)
    {
        double? centralAngle = CentralAngleRadians(latitude1, longitude1, latitude2, longitude2);
        return centralAngle is null ? null : (decimal)(EarthRadiusMetres * centralAngle.Value);
    }

    /// <summary>
    /// The angle the two points subtend at the centre of the earth, in
    /// radians - the unit-agnostic half of the calculation, which each public
    /// overload then scales by the radius it reports in.
    /// </summary>
    private static double? CentralAngleRadians(decimal? latitude1, decimal? longitude1, decimal? latitude2, decimal? longitude2)
    {
        if (latitude1 is null || longitude1 is null || latitude2 is null || longitude2 is null)
        {
            return null;
        }

        double deltaLatitude = ToRadians((double)(latitude2.Value - latitude1.Value));
        double deltaLongitude = ToRadians((double)(longitude2.Value - longitude1.Value));
        double haversine = Math.Sin(deltaLatitude / 2) * Math.Sin(deltaLatitude / 2) +
                           Math.Cos(ToRadians((double)latitude1.Value)) * Math.Cos(ToRadians((double)latitude2.Value)) *
                           Math.Sin(deltaLongitude / 2) * Math.Sin(deltaLongitude / 2);

        // Rounding can leave the haversine term a few ulps outside its
        // mathematical [0,1] range for near-antipodal points, and Math.Sqrt of
        // a negative yields a NaN that the cast to decimal turns into an
        // OverflowException. Clamping costs nothing and cannot move any
        // in-range result.
        double clamped = Math.Clamp(haversine, 0.0, 1.0);
        return 2 * Math.Atan2(Math.Sqrt(clamped), Math.Sqrt(1 - clamped));
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
}

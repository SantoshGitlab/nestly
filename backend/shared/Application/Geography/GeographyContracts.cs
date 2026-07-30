namespace Nestly.Application.Geography;

/// <summary>A city for location selection (SRS 11.1, 11.4.1).</summary>
public record CityResponse(Guid Id, string Name, string StateName);

/// <summary>
/// A locality match within a city, carrying the pincode id that
/// serviceability and slot APIs key off (SRS 11.4.1) - callers resolve a
/// locality by name/pincode text rather than needing to already know its id.
/// </summary>
public record LocalityResponse(Guid Id, string Name, string ZoneName, string PincodeCode, Guid PincodeId);

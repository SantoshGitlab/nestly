namespace Nestly.Application.Geography;

/// <summary>Read-only queries over the geography master for location selection (SRS 12.9.1, 11.4.1).</summary>
public interface IGeographyRepository
{
    Task<bool> CityExistsAsync(Guid cityId);

    /// <summary>Active cities with their state name, alphabetically ordered, for a location picker.</summary>
    Task<IReadOnlyList<CityResponse>> ListActiveCitiesAsync();

    /// <summary>
    /// Active localities within a city, optionally filtered by a
    /// name/pincode substring. Capped to a small page - this backs a
    /// type-ahead picker, not a browsable listing.
    /// </summary>
    Task<IReadOnlyList<LocalityResponse>> SearchActiveLocalitiesAsync(Guid cityId, string? search);

    /// <summary>
    /// Resolves an active pincode's id from its code, or null if no active
    /// pincode has that exact code. Pincode.Code is globally unique (not
    /// scoped to a city), so no city context is needed to disambiguate.
    /// </summary>
    Task<Guid?> FindActivePincodeIdByCodeAsync(string pincodeCode);

    /// <summary>
    /// Resolves the city/state a pincode belongs to, for autofilling an
    /// address form once the customer enters a pincode (task 369) - null if
    /// no active pincode has that exact code, or its city/state is inactive.
    /// </summary>
    Task<PincodeLookupResponse?> ResolvePincodeLocationAsync(string pincodeCode);
}

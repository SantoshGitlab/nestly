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
}

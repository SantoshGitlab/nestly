namespace Nestly.Application.Serviceability;

/// <summary>
/// Read-only queries over the geography master and serviceability mapping
/// tables (SRS 12.9). City is the level category-serviceability is mapped
/// against; pincode is the level service-serviceability is mapped against,
/// with a locality's serviceability following its parent pincode.
/// </summary>
public interface IServiceabilityRepository
{
    Task<bool> CityExistsAsync(Guid cityId);

    Task<bool> PincodeExistsAsync(Guid pincodeId);

    Task<Guid?> GetPincodeIdForLocalityAsync(Guid localityId);

    /// <summary>Resolves the (city, pincode) a locality belongs to, or null if the locality doesn't exist.</summary>
    Task<(Guid CityId, Guid PincodeId)?> GetCityAndPincodeForLocalityAsync(Guid localityId);

    Task<bool> IsCategoryActiveInCityAsync(Guid categoryId, Guid cityId);

    Task<bool> IsServiceActiveInPincodeAsync(Guid serviceId, Guid pincodeId);
}

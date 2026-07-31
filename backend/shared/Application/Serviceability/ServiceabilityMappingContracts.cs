namespace Nestly.Application.Serviceability;

/// <summary>Whether a category is active in a city (SRS 12.9.2), for the admin mapping screen.</summary>
public sealed record CategoryCityMappingResponse(
    Guid Id,
    Guid CategoryId,
    string CategoryName,
    Guid CityId,
    string CityName,
    bool IsActive);

/// <summary>Admin request to map a category as active in a city (SRS 12.9.2).</summary>
public sealed record CategoryCityMappingCreateRequest(Guid CategoryId, Guid CityId);

/// <summary>Whether a service is active in a pincode (SRS 12.9.2), for the admin mapping screen.</summary>
public sealed record ServicePincodeMappingResponse(
    Guid Id,
    Guid ServiceId,
    string ServiceName,
    Guid PincodeId,
    string PincodeCode,
    bool IsActive);

/// <summary>Admin request to map a service as active in a pincode (SRS 12.9.2).</summary>
public sealed record ServicePincodeMappingCreateRequest(Guid ServiceId, Guid PincodeId);

/// <summary>Lightweight category lookup for the mapping admin screen's category picker.</summary>
public sealed record CategoryLookupResponse(Guid Id, string Name);

/// <summary>Lightweight service lookup for the mapping admin screen's service picker.</summary>
public sealed record ServiceLookupResponse(Guid Id, string Name);

namespace Nestly.Application.Geography;

/// <summary>Geography master admin view of a state (SRS 12.9.1).</summary>
public sealed record StateResponse(Guid Id, string Name, string Code, bool IsActive);

/// <summary>Admin create request for a state.</summary>
public sealed record StateCreateRequest(string Name, string Code);

/// <summary>
/// Admin rename request for a state. Renaming only - <see cref="Nestly.Domain.State"/>
/// exposes Rename/Activate/Deactivate but no way to change Code after creation,
/// since Code is the stable classifier other records key off.
/// </summary>
public sealed record StateUpdateRequest(string Name);

/// <summary>Geography master admin view of a city (SRS 12.9.1).</summary>
public sealed record CityAdminResponse(Guid Id, Guid StateId, string StateName, string Name, bool IsActive);

/// <summary>Admin create request for a city.</summary>
public sealed record CityCreateRequest(Guid StateId, string Name);

/// <summary>
/// Admin rename request for a city. Renaming only - <see cref="Nestly.Domain.City"/>
/// has no method to move a city to a different state after creation.
/// </summary>
public sealed record CityUpdateRequest(string Name);

/// <summary>Geography master admin view of a zone (SRS 12.9.1).</summary>
public sealed record ZoneResponse(Guid Id, Guid CityId, string CityName, string Name, bool IsActive);

/// <summary>Admin create request for a zone.</summary>
public sealed record ZoneCreateRequest(Guid CityId, string Name);

/// <summary>
/// Admin rename request for a zone. Renaming only - <see cref="Nestly.Domain.Zone"/>
/// has no method to move a zone to a different city after creation.
/// </summary>
public sealed record ZoneUpdateRequest(string Name);

/// <summary>Geography master admin view of a locality (SRS 12.9.1).</summary>
public sealed record LocalityAdminResponse(
    Guid Id,
    Guid ZoneId,
    string ZoneName,
    Guid PincodeId,
    string PincodeCode,
    string Name,
    bool IsActive);

/// <summary>Admin create request for a locality.</summary>
public sealed record LocalityCreateRequest(Guid ZoneId, Guid PincodeId, string Name);

/// <summary>
/// Admin rename request for a locality. Renaming only - <see cref="Nestly.Domain.Locality"/>
/// has no method to move a locality to a different zone/pincode after creation.
/// </summary>
public sealed record LocalityUpdateRequest(string Name);

/// <summary>Geography master admin view of a pincode (SRS 12.9.1).</summary>
public sealed record PincodeAdminResponse(Guid Id, Guid CityId, string CityName, string Code, bool IsActive);

/// <summary>
/// Admin create request for a pincode. No update/rename request: a
/// pincode's code is a postal identifier, not an editable label - see
/// <see cref="Nestly.Domain.Pincode"/>, which exposes only Activate/Deactivate
/// beyond construction.
/// </summary>
public sealed record PincodeCreateRequest(Guid CityId, string Code);

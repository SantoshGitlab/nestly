using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.Geography;

/// <summary>
/// Admin CRUD over the geography master (SRS 12.9.1): state, city, zone,
/// locality and pincode. Deactivating an entry is the reversible way admins
/// take it out of service without deleting the record it's referenced from
/// (serviceability mappings, addresses, slot capacity).
/// </summary>
public interface IGeographyManagementService
{
    Task<IReadOnlyList<StateResponse>> ListStatesAsync();
    Task<Result<StateResponse>> CreateStateAsync(StateCreateRequest request);
    Task<Result<StateResponse>> UpdateStateAsync(Guid id, StateUpdateRequest request);
    Task<Result> SetStateActiveAsync(Guid id, bool isActive);

    Task<IReadOnlyList<CityAdminResponse>> ListCitiesAsync(Guid? stateId);
    Task<Result<CityAdminResponse>> CreateCityAsync(CityCreateRequest request);
    Task<Result<CityAdminResponse>> UpdateCityAsync(Guid id, CityUpdateRequest request);
    Task<Result> SetCityActiveAsync(Guid id, bool isActive);

    Task<IReadOnlyList<ZoneResponse>> ListZonesAsync(Guid? cityId);
    Task<Result<ZoneResponse>> CreateZoneAsync(ZoneCreateRequest request);
    Task<Result<ZoneResponse>> UpdateZoneAsync(Guid id, ZoneUpdateRequest request);
    Task<Result> SetZoneActiveAsync(Guid id, bool isActive);

    Task<IReadOnlyList<LocalityAdminResponse>> ListLocalitiesAsync(Guid? zoneId);
    Task<Result<LocalityAdminResponse>> CreateLocalityAsync(LocalityCreateRequest request);
    Task<Result<LocalityAdminResponse>> UpdateLocalityAsync(Guid id, LocalityUpdateRequest request);
    Task<Result> SetLocalityActiveAsync(Guid id, bool isActive);

    Task<IReadOnlyList<PincodeAdminResponse>> ListPincodesAsync(Guid? cityId);
    Task<Result<PincodeAdminResponse>> CreatePincodeAsync(PincodeCreateRequest request);
    Task<Result> SetPincodeActiveAsync(Guid id, bool isActive);
}

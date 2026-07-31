using Nestly.Application;
using Nestly.Application.Geography;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;

namespace Nestly.Infrastructure.Services;

/// <summary>Admin CRUD over the geography master (SRS 12.9.1, task 111).</summary>
public class GeographyManagementService : IGeographyManagementService
{
    private readonly IStateRepository _stateRepository;
    private readonly ICityRepository _cityRepository;
    private readonly IZoneRepository _zoneRepository;
    private readonly ILocalityRepository _localityRepository;
    private readonly IPincodeRepository _pincodeRepository;

    public GeographyManagementService(
        IStateRepository stateRepository,
        ICityRepository cityRepository,
        IZoneRepository zoneRepository,
        ILocalityRepository localityRepository,
        IPincodeRepository pincodeRepository)
    {
        _stateRepository = stateRepository;
        _cityRepository = cityRepository;
        _zoneRepository = zoneRepository;
        _localityRepository = localityRepository;
        _pincodeRepository = pincodeRepository;
    }

    public async Task<IReadOnlyList<StateResponse>> ListStatesAsync()
    {
        var states = await _stateRepository.ListAsync();
        return states.Select(ToResponse).ToList();
    }

    public async Task<Result<StateResponse>> CreateStateAsync(StateCreateRequest request)
    {
        if (await _stateRepository.ExistsByCodeAsync(request.Code))
        {
            return Error.Conflict("Geography.DuplicateStateCode", "A state with this code already exists.");
        }

        var state = new State(Guid.NewGuid(), request.Name, request.Code);
        await _stateRepository.AddAsync(state);
        return ToResponse(state);
    }

    public async Task<Result<StateResponse>> UpdateStateAsync(Guid id, StateUpdateRequest request)
    {
        var state = await _stateRepository.GetByIdAsync(id);
        if (state is null)
        {
            return Error.NotFound("Geography.StateNotFound", "The specified state does not exist.");
        }

        state.Rename(request.Name);
        await _stateRepository.UpdateAsync(state);
        return ToResponse(state);
    }

    public async Task<Result> SetStateActiveAsync(Guid id, bool isActive)
    {
        var state = await _stateRepository.GetByIdAsync(id);
        if (state is null)
        {
            return Result.Failure(Error.NotFound("Geography.StateNotFound", "The specified state does not exist."));
        }

        if (isActive) state.Activate(); else state.Deactivate();
        await _stateRepository.UpdateAsync(state);
        return Result.Success();
    }

    public async Task<IReadOnlyList<CityAdminResponse>> ListCitiesAsync(Guid? stateId)
    {
        var cities = await _cityRepository.ListAsync(stateId);
        return cities.Select(ToResponse).ToList();
    }

    public async Task<Result<CityAdminResponse>> CreateCityAsync(CityCreateRequest request)
    {
        var state = await _stateRepository.GetByIdAsync(request.StateId);
        if (state is null)
        {
            return Error.NotFound("Geography.StateNotFound", "The specified state does not exist.");
        }

        if (await _cityRepository.ExistsByNameInStateAsync(request.StateId, request.Name))
        {
            return Error.Conflict("Geography.DuplicateCityName", "A city with this name already exists in this state.");
        }

        var city = new City(Guid.NewGuid(), request.StateId, request.Name);
        await _cityRepository.AddAsync(city);
        return new CityAdminResponse(city.Id, state.Id, state.Name, city.Name, city.IsActive);
    }

    public async Task<Result<CityAdminResponse>> UpdateCityAsync(Guid id, CityUpdateRequest request)
    {
        var city = await _cityRepository.GetByIdAsync(id);
        if (city is null)
        {
            return Error.NotFound("Geography.CityNotFound", "The specified city does not exist.");
        }

        if (await _cityRepository.ExistsByNameInStateAsync(city.StateId, request.Name, excludeId: id))
        {
            return Error.Conflict("Geography.DuplicateCityName", "A city with this name already exists in this state.");
        }

        city.Rename(request.Name);
        await _cityRepository.UpdateAsync(city);

        var state = await _stateRepository.GetByIdAsync(city.StateId);
        return new CityAdminResponse(city.Id, city.StateId, state?.Name ?? string.Empty, city.Name, city.IsActive);
    }

    public async Task<Result> SetCityActiveAsync(Guid id, bool isActive)
    {
        var city = await _cityRepository.GetByIdAsync(id);
        if (city is null)
        {
            return Result.Failure(Error.NotFound("Geography.CityNotFound", "The specified city does not exist."));
        }

        if (isActive) city.Activate(); else city.Deactivate();
        await _cityRepository.UpdateAsync(city);
        return Result.Success();
    }

    public async Task<IReadOnlyList<ZoneResponse>> ListZonesAsync(Guid? cityId)
    {
        var zones = await _zoneRepository.ListAsync(cityId);
        return zones.Select(ToResponse).ToList();
    }

    public async Task<Result<ZoneResponse>> CreateZoneAsync(ZoneCreateRequest request)
    {
        var city = await _cityRepository.GetByIdAsync(request.CityId);
        if (city is null)
        {
            return Error.NotFound("Geography.CityNotFound", "The specified city does not exist.");
        }

        if (await _zoneRepository.ExistsByNameInCityAsync(request.CityId, request.Name))
        {
            return Error.Conflict("Geography.DuplicateZoneName", "A zone with this name already exists in this city.");
        }

        var zone = new Zone(Guid.NewGuid(), request.CityId, request.Name);
        await _zoneRepository.AddAsync(zone);
        return new ZoneResponse(zone.Id, city.Id, city.Name, zone.Name, zone.IsActive);
    }

    public async Task<Result<ZoneResponse>> UpdateZoneAsync(Guid id, ZoneUpdateRequest request)
    {
        var zone = await _zoneRepository.GetByIdAsync(id);
        if (zone is null)
        {
            return Error.NotFound("Geography.ZoneNotFound", "The specified zone does not exist.");
        }

        if (await _zoneRepository.ExistsByNameInCityAsync(zone.CityId, request.Name, excludeId: id))
        {
            return Error.Conflict("Geography.DuplicateZoneName", "A zone with this name already exists in this city.");
        }

        zone.Rename(request.Name);
        await _zoneRepository.UpdateAsync(zone);

        var city = await _cityRepository.GetByIdAsync(zone.CityId);
        return new ZoneResponse(zone.Id, zone.CityId, city?.Name ?? string.Empty, zone.Name, zone.IsActive);
    }

    public async Task<Result> SetZoneActiveAsync(Guid id, bool isActive)
    {
        var zone = await _zoneRepository.GetByIdAsync(id);
        if (zone is null)
        {
            return Result.Failure(Error.NotFound("Geography.ZoneNotFound", "The specified zone does not exist."));
        }

        if (isActive) zone.Activate(); else zone.Deactivate();
        await _zoneRepository.UpdateAsync(zone);
        return Result.Success();
    }

    public async Task<IReadOnlyList<LocalityAdminResponse>> ListLocalitiesAsync(Guid? zoneId)
    {
        var localities = await _localityRepository.ListAsync(zoneId);
        return localities.Select(ToResponse).ToList();
    }

    public async Task<Result<LocalityAdminResponse>> CreateLocalityAsync(LocalityCreateRequest request)
    {
        var zone = await _zoneRepository.GetByIdAsync(request.ZoneId);
        if (zone is null)
        {
            return Error.NotFound("Geography.ZoneNotFound", "The specified zone does not exist.");
        }

        var pincode = await _pincodeRepository.GetByIdAsync(request.PincodeId);
        if (pincode is null)
        {
            return Error.NotFound("Geography.PincodeNotFound", "The specified pincode does not exist.");
        }

        if (await _localityRepository.ExistsByNameInZoneAsync(request.ZoneId, request.Name))
        {
            return Error.Conflict("Geography.DuplicateLocalityName", "A locality with this name already exists in this zone.");
        }

        var locality = new Locality(Guid.NewGuid(), request.ZoneId, request.PincodeId, request.Name);
        await _localityRepository.AddAsync(locality);
        return new LocalityAdminResponse(
            locality.Id, zone.Id, zone.Name, pincode.Id, pincode.Code, locality.Name, locality.IsActive);
    }

    public async Task<Result<LocalityAdminResponse>> UpdateLocalityAsync(Guid id, LocalityUpdateRequest request)
    {
        var locality = await _localityRepository.GetByIdAsync(id);
        if (locality is null)
        {
            return Error.NotFound("Geography.LocalityNotFound", "The specified locality does not exist.");
        }

        if (await _localityRepository.ExistsByNameInZoneAsync(locality.ZoneId, request.Name, excludeId: id))
        {
            return Error.Conflict("Geography.DuplicateLocalityName", "A locality with this name already exists in this zone.");
        }

        locality.Rename(request.Name);
        await _localityRepository.UpdateAsync(locality);

        var zone = await _zoneRepository.GetByIdAsync(locality.ZoneId);
        var pincode = await _pincodeRepository.GetByIdAsync(locality.PincodeId);
        return new LocalityAdminResponse(
            locality.Id, locality.ZoneId, zone?.Name ?? string.Empty,
            locality.PincodeId, pincode?.Code ?? string.Empty, locality.Name, locality.IsActive);
    }

    public async Task<Result> SetLocalityActiveAsync(Guid id, bool isActive)
    {
        var locality = await _localityRepository.GetByIdAsync(id);
        if (locality is null)
        {
            return Result.Failure(Error.NotFound("Geography.LocalityNotFound", "The specified locality does not exist."));
        }

        if (isActive) locality.Activate(); else locality.Deactivate();
        await _localityRepository.UpdateAsync(locality);
        return Result.Success();
    }

    public async Task<IReadOnlyList<PincodeAdminResponse>> ListPincodesAsync(Guid? cityId)
    {
        var pincodes = await _pincodeRepository.ListAsync(cityId);
        return pincodes.Select(ToResponse).ToList();
    }

    public async Task<Result<PincodeAdminResponse>> CreatePincodeAsync(PincodeCreateRequest request)
    {
        var city = await _cityRepository.GetByIdAsync(request.CityId);
        if (city is null)
        {
            return Error.NotFound("Geography.CityNotFound", "The specified city does not exist.");
        }

        if (await _pincodeRepository.ExistsByCodeAsync(request.Code))
        {
            return Error.Conflict("Geography.DuplicatePincodeCode", "A pincode with this code already exists.");
        }

        var pincode = new Pincode(Guid.NewGuid(), request.CityId, request.Code);
        await _pincodeRepository.AddAsync(pincode);
        return new PincodeAdminResponse(pincode.Id, city.Id, city.Name, pincode.Code, pincode.IsActive);
    }

    public async Task<Result> SetPincodeActiveAsync(Guid id, bool isActive)
    {
        var pincode = await _pincodeRepository.GetByIdAsync(id);
        if (pincode is null)
        {
            return Result.Failure(Error.NotFound("Geography.PincodeNotFound", "The specified pincode does not exist."));
        }

        if (isActive) pincode.Activate(); else pincode.Deactivate();
        await _pincodeRepository.UpdateAsync(pincode);
        return Result.Success();
    }

    private static StateResponse ToResponse(State state) =>
        new(state.Id, state.Name, state.Code, state.IsActive);

    private static CityAdminResponse ToResponse(City city) =>
        new(city.Id, city.StateId, city.State?.Name ?? string.Empty, city.Name, city.IsActive);

    private static ZoneResponse ToResponse(Zone zone) =>
        new(zone.Id, zone.CityId, zone.City?.Name ?? string.Empty, zone.Name, zone.IsActive);

    private static LocalityAdminResponse ToResponse(Locality locality) =>
        new(locality.Id, locality.ZoneId, locality.Zone?.Name ?? string.Empty,
            locality.PincodeId, locality.Pincode?.Code ?? string.Empty, locality.Name, locality.IsActive);

    private static PincodeAdminResponse ToResponse(Pincode pincode) =>
        new(pincode.Id, pincode.CityId, pincode.City?.Name ?? string.Empty, pincode.Code, pincode.IsActive);
}

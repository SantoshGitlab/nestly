using Asp.Versioning;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Nestly.Application.Geography;
using Nestly.BuildingBlocks.Extensions;
using Nestly.Domain;
using Nestly.Infrastructure;

namespace Nestly.AdminApi.Controllers;

/// <summary>
/// Admin geography master CRUD (SRS 12.9.1, task 111): state, city, zone,
/// locality and pincode. Gated behind the "serviceability" module - the
/// geography master and the serviceability mappings built on top of it
/// (see <see cref="ServiceabilityMappingsController"/>) are one admin
/// capability (SRS 12.9).
/// </summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/admin/geography")]
[Authorize(AuthenticationSchemes = DependencyInjection.AdminJwtBearerScheme)]
public class GeographyController : ControllerBase
{
    private const string ReadPolicy = AdminModules.Serviceability + ".read";
    private const string WritePolicy = AdminModules.Serviceability + ".write";

    private readonly IGeographyManagementService _geographyManagementService;
    private readonly IValidator<StateCreateRequest> _stateCreateValidator;
    private readonly IValidator<StateUpdateRequest> _stateUpdateValidator;
    private readonly IValidator<CityCreateRequest> _cityCreateValidator;
    private readonly IValidator<CityUpdateRequest> _cityUpdateValidator;
    private readonly IValidator<ZoneCreateRequest> _zoneCreateValidator;
    private readonly IValidator<ZoneUpdateRequest> _zoneUpdateValidator;
    private readonly IValidator<LocalityCreateRequest> _localityCreateValidator;
    private readonly IValidator<LocalityUpdateRequest> _localityUpdateValidator;
    private readonly IValidator<PincodeCreateRequest> _pincodeCreateValidator;

    public GeographyController(
        IGeographyManagementService geographyManagementService,
        IValidator<StateCreateRequest> stateCreateValidator,
        IValidator<StateUpdateRequest> stateUpdateValidator,
        IValidator<CityCreateRequest> cityCreateValidator,
        IValidator<CityUpdateRequest> cityUpdateValidator,
        IValidator<ZoneCreateRequest> zoneCreateValidator,
        IValidator<ZoneUpdateRequest> zoneUpdateValidator,
        IValidator<LocalityCreateRequest> localityCreateValidator,
        IValidator<LocalityUpdateRequest> localityUpdateValidator,
        IValidator<PincodeCreateRequest> pincodeCreateValidator)
    {
        _geographyManagementService = geographyManagementService;
        _stateCreateValidator = stateCreateValidator;
        _stateUpdateValidator = stateUpdateValidator;
        _cityCreateValidator = cityCreateValidator;
        _cityUpdateValidator = cityUpdateValidator;
        _zoneCreateValidator = zoneCreateValidator;
        _zoneUpdateValidator = zoneUpdateValidator;
        _localityCreateValidator = localityCreateValidator;
        _localityUpdateValidator = localityUpdateValidator;
        _pincodeCreateValidator = pincodeCreateValidator;
    }

    // ---- States ----

    [HttpGet("states")]
    [Authorize(Policy = ReadPolicy)]
    [ProducesResponseType(typeof(IReadOnlyList<StateResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListStates() => Ok(await _geographyManagementService.ListStatesAsync());

    [HttpPost("states")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(typeof(StateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateState([FromBody] StateCreateRequest request)
    {
        var validation = await _stateCreateValidator.ValidateAsync(request);
        if (!validation.IsValid) return ValidationProblem(ToModelState(validation));

        var result = await _geographyManagementService.CreateStateAsync(request);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    [HttpPut("states/{id:guid}")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(typeof(StateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateState(Guid id, [FromBody] StateUpdateRequest request)
    {
        var validation = await _stateUpdateValidator.ValidateAsync(request);
        if (!validation.IsValid) return ValidationProblem(ToModelState(validation));

        var result = await _geographyManagementService.UpdateStateAsync(id, request);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    [HttpPost("states/{id:guid}/activate")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ActivateState(Guid id)
    {
        var result = await _geographyManagementService.SetStateActiveAsync(id, true);
        return result.IsSuccess ? NoContent() : result.ToProblemResult();
    }

    [HttpPost("states/{id:guid}/deactivate")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivateState(Guid id)
    {
        var result = await _geographyManagementService.SetStateActiveAsync(id, false);
        return result.IsSuccess ? NoContent() : result.ToProblemResult();
    }

    // ---- Cities ----

    [HttpGet("cities")]
    [Authorize(Policy = ReadPolicy)]
    [ProducesResponseType(typeof(IReadOnlyList<CityAdminResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListCities([FromQuery] Guid? stateId) =>
        Ok(await _geographyManagementService.ListCitiesAsync(stateId));

    [HttpPost("cities")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(typeof(CityAdminResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateCity([FromBody] CityCreateRequest request)
    {
        var validation = await _cityCreateValidator.ValidateAsync(request);
        if (!validation.IsValid) return ValidationProblem(ToModelState(validation));

        var result = await _geographyManagementService.CreateCityAsync(request);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    [HttpPut("cities/{id:guid}")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(typeof(CityAdminResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateCity(Guid id, [FromBody] CityUpdateRequest request)
    {
        var validation = await _cityUpdateValidator.ValidateAsync(request);
        if (!validation.IsValid) return ValidationProblem(ToModelState(validation));

        var result = await _geographyManagementService.UpdateCityAsync(id, request);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    [HttpPost("cities/{id:guid}/activate")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ActivateCity(Guid id)
    {
        var result = await _geographyManagementService.SetCityActiveAsync(id, true);
        return result.IsSuccess ? NoContent() : result.ToProblemResult();
    }

    [HttpPost("cities/{id:guid}/deactivate")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivateCity(Guid id)
    {
        var result = await _geographyManagementService.SetCityActiveAsync(id, false);
        return result.IsSuccess ? NoContent() : result.ToProblemResult();
    }

    // ---- Zones ----

    [HttpGet("zones")]
    [Authorize(Policy = ReadPolicy)]
    [ProducesResponseType(typeof(IReadOnlyList<ZoneResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListZones([FromQuery] Guid? cityId) =>
        Ok(await _geographyManagementService.ListZonesAsync(cityId));

    [HttpPost("zones")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(typeof(ZoneResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateZone([FromBody] ZoneCreateRequest request)
    {
        var validation = await _zoneCreateValidator.ValidateAsync(request);
        if (!validation.IsValid) return ValidationProblem(ToModelState(validation));

        var result = await _geographyManagementService.CreateZoneAsync(request);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    [HttpPut("zones/{id:guid}")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(typeof(ZoneResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateZone(Guid id, [FromBody] ZoneUpdateRequest request)
    {
        var validation = await _zoneUpdateValidator.ValidateAsync(request);
        if (!validation.IsValid) return ValidationProblem(ToModelState(validation));

        var result = await _geographyManagementService.UpdateZoneAsync(id, request);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    [HttpPost("zones/{id:guid}/activate")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ActivateZone(Guid id)
    {
        var result = await _geographyManagementService.SetZoneActiveAsync(id, true);
        return result.IsSuccess ? NoContent() : result.ToProblemResult();
    }

    [HttpPost("zones/{id:guid}/deactivate")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivateZone(Guid id)
    {
        var result = await _geographyManagementService.SetZoneActiveAsync(id, false);
        return result.IsSuccess ? NoContent() : result.ToProblemResult();
    }

    // ---- Localities ----

    [HttpGet("localities")]
    [Authorize(Policy = ReadPolicy)]
    [ProducesResponseType(typeof(IReadOnlyList<LocalityAdminResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListLocalities([FromQuery] Guid? zoneId) =>
        Ok(await _geographyManagementService.ListLocalitiesAsync(zoneId));

    [HttpPost("localities")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(typeof(LocalityAdminResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateLocality([FromBody] LocalityCreateRequest request)
    {
        var validation = await _localityCreateValidator.ValidateAsync(request);
        if (!validation.IsValid) return ValidationProblem(ToModelState(validation));

        var result = await _geographyManagementService.CreateLocalityAsync(request);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    [HttpPut("localities/{id:guid}")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(typeof(LocalityAdminResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateLocality(Guid id, [FromBody] LocalityUpdateRequest request)
    {
        var validation = await _localityUpdateValidator.ValidateAsync(request);
        if (!validation.IsValid) return ValidationProblem(ToModelState(validation));

        var result = await _geographyManagementService.UpdateLocalityAsync(id, request);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    [HttpPost("localities/{id:guid}/activate")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ActivateLocality(Guid id)
    {
        var result = await _geographyManagementService.SetLocalityActiveAsync(id, true);
        return result.IsSuccess ? NoContent() : result.ToProblemResult();
    }

    [HttpPost("localities/{id:guid}/deactivate")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivateLocality(Guid id)
    {
        var result = await _geographyManagementService.SetLocalityActiveAsync(id, false);
        return result.IsSuccess ? NoContent() : result.ToProblemResult();
    }

    // ---- Pincodes ----

    [HttpGet("pincodes")]
    [Authorize(Policy = ReadPolicy)]
    [ProducesResponseType(typeof(IReadOnlyList<PincodeAdminResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListPincodes([FromQuery] Guid? cityId) =>
        Ok(await _geographyManagementService.ListPincodesAsync(cityId));

    [HttpPost("pincodes")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(typeof(PincodeAdminResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreatePincode([FromBody] PincodeCreateRequest request)
    {
        var validation = await _pincodeCreateValidator.ValidateAsync(request);
        if (!validation.IsValid) return ValidationProblem(ToModelState(validation));

        var result = await _geographyManagementService.CreatePincodeAsync(request);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    [HttpPost("pincodes/{id:guid}/activate")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ActivatePincode(Guid id)
    {
        var result = await _geographyManagementService.SetPincodeActiveAsync(id, true);
        return result.IsSuccess ? NoContent() : result.ToProblemResult();
    }

    [HttpPost("pincodes/{id:guid}/deactivate")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivatePincode(Guid id)
    {
        var result = await _geographyManagementService.SetPincodeActiveAsync(id, false);
        return result.IsSuccess ? NoContent() : result.ToProblemResult();
    }

    private static ModelStateDictionary ToModelState(ValidationResult validation)
    {
        var modelState = new ModelStateDictionary();
        foreach (var error in validation.Errors)
        {
            modelState.AddModelError(error.PropertyName, error.ErrorMessage);
        }

        return modelState;
    }
}

using Asp.Versioning;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Nestly.Application.Slots;
using Nestly.BuildingBlocks.Extensions;
using Nestly.Domain;
using Nestly.Infrastructure;

namespace Nestly.AdminApi.Controllers;

/// <summary>
/// Admin slot configuration (SRS 12.10, tasks 113a-e): recurring windows and
/// their day-of-week rules, holiday/blackout dates, per-city booking cutoffs
/// and advance-booking limits, per-window capacity, and one-off availability
/// overrides. Gated behind the "slots" permission module - distinct from
/// <see cref="Nestly.ConsumerApi.Controllers.SlotsController"/>, which serves
/// the unauthenticated customer-facing availability computed from this
/// configuration.
/// </summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/admin/slots")]
[Authorize(AuthenticationSchemes = DependencyInjection.AdminJwtBearerScheme)]
public class SlotsController : ControllerBase
{
    private const string ReadPolicy = AdminModules.Slots + ".read";
    private const string WritePolicy = AdminModules.Slots + ".write";

    private readonly ISlotManagementService _slotManagementService;
    private readonly IValidator<SlotWindowCreateRequest> _windowCreateValidator;
    private readonly IValidator<SlotWindowUpdateRequest> _windowUpdateValidator;
    private readonly IValidator<SlotWindowCapacityUpdateRequest> _windowCapacityValidator;
    private readonly IValidator<SlotBlackoutCreateRequest> _blackoutCreateValidator;
    private readonly IValidator<SlotBookingPolicyUpsertRequest> _bookingPolicyUpsertValidator;
    private readonly IValidator<SlotAvailabilityOverrideCreateRequest> _overrideCreateValidator;

    public SlotsController(
        ISlotManagementService slotManagementService,
        IValidator<SlotWindowCreateRequest> windowCreateValidator,
        IValidator<SlotWindowUpdateRequest> windowUpdateValidator,
        IValidator<SlotWindowCapacityUpdateRequest> windowCapacityValidator,
        IValidator<SlotBlackoutCreateRequest> blackoutCreateValidator,
        IValidator<SlotBookingPolicyUpsertRequest> bookingPolicyUpsertValidator,
        IValidator<SlotAvailabilityOverrideCreateRequest> overrideCreateValidator)
    {
        _slotManagementService = slotManagementService;
        _windowCreateValidator = windowCreateValidator;
        _windowUpdateValidator = windowUpdateValidator;
        _windowCapacityValidator = windowCapacityValidator;
        _blackoutCreateValidator = blackoutCreateValidator;
        _bookingPolicyUpsertValidator = bookingPolicyUpsertValidator;
        _overrideCreateValidator = overrideCreateValidator;
    }

    // ---- Lookups ----

    [HttpGet("cities")]
    [Authorize(Policy = ReadPolicy)]
    [ProducesResponseType(typeof(IReadOnlyList<SlotCityLookupResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListCities() => Ok(await _slotManagementService.ListCityLookupsAsync());

    [HttpGet("categories")]
    [Authorize(Policy = ReadPolicy)]
    [ProducesResponseType(typeof(IReadOnlyList<Nestly.Application.Serviceability.CategoryLookupResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListCategories() => Ok(await _slotManagementService.ListCategoryLookupsAsync());

    [HttpGet("services")]
    [Authorize(Policy = ReadPolicy)]
    [ProducesResponseType(typeof(IReadOnlyList<Nestly.Application.Serviceability.ServiceLookupResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListServices() => Ok(await _slotManagementService.ListServiceLookupsAsync());

    // ---- Windows (task 113a) ----

    [HttpGet("windows")]
    [Authorize(Policy = ReadPolicy)]
    [ProducesResponseType(typeof(IReadOnlyList<SlotWindowAdminResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListWindows([FromQuery] Guid? cityId) =>
        Ok(await _slotManagementService.ListWindowsAsync(cityId));

    [HttpPost("windows")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(typeof(SlotWindowAdminResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateWindow([FromBody] SlotWindowCreateRequest request)
    {
        var validation = await _windowCreateValidator.ValidateAsync(request);
        if (!validation.IsValid) return ValidationProblem(ToModelState(validation));

        var result = await _slotManagementService.CreateWindowAsync(request);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    [HttpPut("windows/{id:guid}")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(typeof(SlotWindowAdminResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateWindow(Guid id, [FromBody] SlotWindowUpdateRequest request)
    {
        var validation = await _windowUpdateValidator.ValidateAsync(request);
        if (!validation.IsValid) return ValidationProblem(ToModelState(validation));

        var result = await _slotManagementService.UpdateWindowAsync(id, request);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    [HttpPatch("windows/{id:guid}/capacity")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(typeof(SlotWindowAdminResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SetWindowCapacity(Guid id, [FromBody] SlotWindowCapacityUpdateRequest request)
    {
        var validation = await _windowCapacityValidator.ValidateAsync(request);
        if (!validation.IsValid) return ValidationProblem(ToModelState(validation));

        var result = await _slotManagementService.SetWindowCapacityAsync(id, request);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    [HttpPost("windows/{id:guid}/activate")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ActivateWindow(Guid id)
    {
        var result = await _slotManagementService.SetWindowActiveAsync(id, true);
        return result.IsSuccess ? NoContent() : result.ToProblemResult();
    }

    [HttpPost("windows/{id:guid}/deactivate")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivateWindow(Guid id)
    {
        var result = await _slotManagementService.SetWindowActiveAsync(id, false);
        return result.IsSuccess ? NoContent() : result.ToProblemResult();
    }

    // ---- Blackouts (task 113b) ----

    [HttpGet("blackouts")]
    [Authorize(Policy = ReadPolicy)]
    [ProducesResponseType(typeof(IReadOnlyList<SlotBlackoutAdminResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListBlackouts([FromQuery] Guid? cityId) =>
        Ok(await _slotManagementService.ListBlackoutsAsync(cityId));

    [HttpPost("blackouts")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(typeof(SlotBlackoutAdminResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateBlackout([FromBody] SlotBlackoutCreateRequest request)
    {
        var validation = await _blackoutCreateValidator.ValidateAsync(request);
        if (!validation.IsValid) return ValidationProblem(ToModelState(validation));

        var result = await _slotManagementService.CreateBlackoutAsync(request);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    [HttpDelete("blackouts/{id:guid}")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteBlackout(Guid id)
    {
        var result = await _slotManagementService.DeleteBlackoutAsync(id);
        return result.IsSuccess ? NoContent() : result.ToProblemResult();
    }

    // ---- Cutoffs / advance-booking policy (task 113c) ----

    [HttpGet("booking-policies")]
    [Authorize(Policy = ReadPolicy)]
    [ProducesResponseType(typeof(IReadOnlyList<SlotBookingPolicyAdminResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListBookingPolicies() => Ok(await _slotManagementService.ListBookingPoliciesAsync());

    [HttpPut("booking-policies")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(typeof(SlotBookingPolicyAdminResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpsertBookingPolicy([FromBody] SlotBookingPolicyUpsertRequest request)
    {
        var validation = await _bookingPolicyUpsertValidator.ValidateAsync(request);
        if (!validation.IsValid) return ValidationProblem(ToModelState(validation));

        var result = await _slotManagementService.UpsertBookingPolicyAsync(request);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    // ---- Availability overrides (task 113e, SRS 12.10.2) ----

    [HttpGet("overrides")]
    [Authorize(Policy = ReadPolicy)]
    [ProducesResponseType(typeof(IReadOnlyList<SlotAvailabilityOverrideAdminResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListOverrides([FromQuery] Guid? cityId, [FromQuery] DateOnly? date) =>
        Ok(await _slotManagementService.ListOverridesAsync(cityId, date));

    [HttpPost("overrides")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(typeof(SlotAvailabilityOverrideAdminResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateOverride([FromBody] SlotAvailabilityOverrideCreateRequest request)
    {
        var validation = await _overrideCreateValidator.ValidateAsync(request);
        if (!validation.IsValid) return ValidationProblem(ToModelState(validation));

        var result = await _slotManagementService.CreateOverrideAsync(request);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    [HttpDelete("overrides/{id:guid}")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteOverride(Guid id)
    {
        var result = await _slotManagementService.DeleteOverrideAsync(id);
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

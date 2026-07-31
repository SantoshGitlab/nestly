using Asp.Versioning;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Nestly.Application.Pricing;
using Nestly.BuildingBlocks.Extensions;
using Nestly.Domain;
using Nestly.Infrastructure;

namespace Nestly.AdminApi.Controllers;

/// <summary>
/// Admin pricing management (SRS 12.8, tasks 109a-109e): base/add-on/
/// city-wise/promotional price, per-city tax rate, visit charge and
/// platform fee, effective dating, and a price-change audit trail via
/// <c>IAuditLogWriter</c>. Gated behind the "pricing" module, matching the
/// admin RBAC catalog in <see cref="AdminPermissionCatalog"/>.
/// </summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/admin/pricing")]
[Authorize(AuthenticationSchemes = DependencyInjection.AdminJwtBearerScheme)]
public class PricingController : ControllerBase
{
    private const string ReadPolicy = AdminModules.Pricing + ".read";
    private const string WritePolicy = AdminModules.Pricing + ".write";

    private readonly IPricingManagementService _pricingManagementService;
    private readonly IValidator<ServicePriceUpdateRequest> _servicePriceUpdateValidator;
    private readonly IValidator<AddOnPriceUpdateRequest> _addOnPriceUpdateValidator;
    private readonly IValidator<CityPriceCreateRequest> _cityPriceCreateValidator;
    private readonly IValidator<CityPriceUpdateRequest> _cityPriceUpdateValidator;
    private readonly IValidator<PromotionalPriceCreateRequest> _promotionalPriceCreateValidator;
    private readonly IValidator<PromotionalPriceUpdateRequest> _promotionalPriceUpdateValidator;
    private readonly IValidator<CityPricingPolicyUpsertRequest> _cityPricingPolicyUpsertValidator;

    public PricingController(
        IPricingManagementService pricingManagementService,
        IValidator<ServicePriceUpdateRequest> servicePriceUpdateValidator,
        IValidator<AddOnPriceUpdateRequest> addOnPriceUpdateValidator,
        IValidator<CityPriceCreateRequest> cityPriceCreateValidator,
        IValidator<CityPriceUpdateRequest> cityPriceUpdateValidator,
        IValidator<PromotionalPriceCreateRequest> promotionalPriceCreateValidator,
        IValidator<PromotionalPriceUpdateRequest> promotionalPriceUpdateValidator,
        IValidator<CityPricingPolicyUpsertRequest> cityPricingPolicyUpsertValidator)
    {
        _pricingManagementService = pricingManagementService;
        _servicePriceUpdateValidator = servicePriceUpdateValidator;
        _addOnPriceUpdateValidator = addOnPriceUpdateValidator;
        _cityPriceCreateValidator = cityPriceCreateValidator;
        _cityPriceUpdateValidator = cityPriceUpdateValidator;
        _promotionalPriceCreateValidator = promotionalPriceCreateValidator;
        _promotionalPriceUpdateValidator = promotionalPriceUpdateValidator;
        _cityPricingPolicyUpsertValidator = cityPricingPolicyUpsertValidator;
    }

    // ---- Base price (task 109a) ----

    [HttpGet("services")]
    [Authorize(Policy = ReadPolicy)]
    [ProducesResponseType(typeof(IReadOnlyList<ServicePriceResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListServicePrices(CancellationToken cancellationToken) =>
        Ok(await _pricingManagementService.ListServicePricesAsync(cancellationToken));

    [HttpPut("services/{serviceId:guid}")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(typeof(ServicePriceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateServicePrice(Guid serviceId, [FromBody] ServicePriceUpdateRequest request, CancellationToken cancellationToken)
    {
        var validation = await _servicePriceUpdateValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) return ValidationProblem(ToModelState(validation));

        var result = await _pricingManagementService.UpdateServicePriceAsync(serviceId, request, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    // ---- Add-on price (task 109a) ----

    [HttpGet("addons")]
    [Authorize(Policy = ReadPolicy)]
    [ProducesResponseType(typeof(IReadOnlyList<AddOnPriceResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAddOnPrices([FromQuery] Guid? serviceId, CancellationToken cancellationToken) =>
        Ok(await _pricingManagementService.ListAddOnPricesAsync(serviceId, cancellationToken));

    [HttpPut("addons/{addOnId:guid}")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(typeof(AddOnPriceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateAddOnPrice(Guid addOnId, [FromBody] AddOnPriceUpdateRequest request, CancellationToken cancellationToken)
    {
        var validation = await _addOnPriceUpdateValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) return ValidationProblem(ToModelState(validation));

        var result = await _pricingManagementService.UpdateAddOnPriceAsync(addOnId, request, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    // ---- City-wise price (task 109a, effective dates task 109d) ----

    [HttpGet("city-prices")]
    [Authorize(Policy = ReadPolicy)]
    [ProducesResponseType(typeof(IReadOnlyList<CityPriceResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListCityPrices([FromQuery] Guid? serviceId, [FromQuery] Guid? cityId, CancellationToken cancellationToken) =>
        Ok(await _pricingManagementService.ListCityPricesAsync(serviceId, cityId, cancellationToken));

    [HttpPost("city-prices")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(typeof(CityPriceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateCityPrice([FromBody] CityPriceCreateRequest request, CancellationToken cancellationToken)
    {
        var validation = await _cityPriceCreateValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) return ValidationProblem(ToModelState(validation));

        var result = await _pricingManagementService.CreateCityPriceAsync(request, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    [HttpPut("city-prices/{id:guid}")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(typeof(CityPriceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateCityPrice(Guid id, [FromBody] CityPriceUpdateRequest request, CancellationToken cancellationToken)
    {
        var validation = await _cityPriceUpdateValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) return ValidationProblem(ToModelState(validation));

        var result = await _pricingManagementService.UpdateCityPriceAsync(id, request, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    // ---- Promotional price (task 109a) ----

    [HttpGet("promotions")]
    [Authorize(Policy = ReadPolicy)]
    [ProducesResponseType(typeof(IReadOnlyList<PromotionalPriceResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListPromotionalPrices([FromQuery] Guid? serviceId, CancellationToken cancellationToken) =>
        Ok(await _pricingManagementService.ListPromotionalPricesAsync(serviceId, cancellationToken));

    [HttpPost("promotions")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(typeof(PromotionalPriceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreatePromotionalPrice([FromBody] PromotionalPriceCreateRequest request, CancellationToken cancellationToken)
    {
        var validation = await _promotionalPriceCreateValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) return ValidationProblem(ToModelState(validation));

        var result = await _pricingManagementService.CreatePromotionalPriceAsync(request, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    [HttpPut("promotions/{id:guid}")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(typeof(PromotionalPriceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdatePromotionalPrice(Guid id, [FromBody] PromotionalPriceUpdateRequest request, CancellationToken cancellationToken)
    {
        var validation = await _promotionalPriceUpdateValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) return ValidationProblem(ToModelState(validation));

        var result = await _pricingManagementService.UpdatePromotionalPriceAsync(id, request, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    [HttpPost("promotions/{id:guid}/activate")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ActivatePromotionalPrice(Guid id, CancellationToken cancellationToken)
    {
        var result = await _pricingManagementService.SetPromotionalPriceActiveAsync(id, true, cancellationToken);
        return result.IsSuccess ? NoContent() : result.ToProblemResult();
    }

    [HttpPost("promotions/{id:guid}/deactivate")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivatePromotionalPrice(Guid id, CancellationToken cancellationToken)
    {
        var result = await _pricingManagementService.SetPromotionalPriceActiveAsync(id, false, cancellationToken);
        return result.IsSuccess ? NoContent() : result.ToProblemResult();
    }

    // ---- City pricing policy: tax + fees (tasks 109b/109c) ----

    [HttpGet("policies")]
    [Authorize(Policy = ReadPolicy)]
    [ProducesResponseType(typeof(IReadOnlyList<CityPricingPolicyResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListCityPricingPolicies(CancellationToken cancellationToken) =>
        Ok(await _pricingManagementService.ListCityPricingPoliciesAsync(cancellationToken));

    [HttpPut("policies/{cityId:guid}")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(typeof(CityPricingPolicyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpsertCityPricingPolicy(Guid cityId, [FromBody] CityPricingPolicyUpsertRequest request, CancellationToken cancellationToken)
    {
        var validation = await _cityPricingPolicyUpsertValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) return ValidationProblem(ToModelState(validation));

        var result = await _pricingManagementService.UpsertCityPricingPolicyAsync(cityId, request, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
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

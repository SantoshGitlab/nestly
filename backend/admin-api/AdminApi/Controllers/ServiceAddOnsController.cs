using Asp.Versioning;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Nestly.Application.Catalog;
using Nestly.BuildingBlocks.Extensions;
using Nestly.Domain;
using Nestly.Infrastructure;

namespace Nestly.AdminApi.Controllers;

/// <summary>
/// Admin add-on management (SRS 12.7, task 107): CRUD and mapping to
/// services, activation. Gated behind the "catalog" permission module, same
/// as <see cref="CategoriesController"/>/<see cref="ServicesController"/>
/// (SRS 12.5-12.7 share one module).
/// </summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/admin/catalog/addons")]
[Authorize(AuthenticationSchemes = DependencyInjection.AdminJwtBearerScheme)]
public class ServiceAddOnsController : ControllerBase
{
    private const string ReadPolicy = AdminModules.Catalog + ".read";
    private const string WritePolicy = AdminModules.Catalog + ".write";

    private readonly IServiceAddOnManagementService _addOnManagementService;
    private readonly IValidator<ServiceAddOnCreateRequest> _createValidator;
    private readonly IValidator<ServiceAddOnUpdateRequest> _updateValidator;

    public ServiceAddOnsController(
        IServiceAddOnManagementService addOnManagementService,
        IValidator<ServiceAddOnCreateRequest> createValidator,
        IValidator<ServiceAddOnUpdateRequest> updateValidator)
    {
        _addOnManagementService = addOnManagementService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    [HttpGet]
    [Authorize(Policy = ReadPolicy)]
    [ProducesResponseType(typeof(IReadOnlyList<ServiceAddOnAdminResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] Guid? serviceId) =>
        Ok(await _addOnManagementService.ListAsync(serviceId));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = ReadPolicy)]
    [ProducesResponseType(typeof(ServiceAddOnAdminResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _addOnManagementService.GetByIdAsync(id);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    [HttpPost]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(typeof(ServiceAddOnAdminResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create([FromBody] ServiceAddOnCreateRequest request)
    {
        var validation = await _createValidator.ValidateAsync(request);
        if (!validation.IsValid) return ValidationProblem(ToModelState(validation));

        var result = await _addOnManagementService.CreateAsync(request);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(typeof(ServiceAddOnAdminResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(Guid id, [FromBody] ServiceAddOnUpdateRequest request)
    {
        var validation = await _updateValidator.ValidateAsync(request);
        if (!validation.IsValid) return ValidationProblem(ToModelState(validation));

        var result = await _addOnManagementService.UpdateAsync(id, request);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    [HttpPost("{id:guid}/activate")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Activate(Guid id)
    {
        var result = await _addOnManagementService.SetActiveAsync(id, true);
        return result.IsSuccess ? NoContent() : result.ToProblemResult();
    }

    [HttpPost("{id:guid}/deactivate")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        var result = await _addOnManagementService.SetActiveAsync(id, false);
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

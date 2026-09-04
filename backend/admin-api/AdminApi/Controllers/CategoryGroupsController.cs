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
/// Admin management of category groups - optional section headers for a
/// subset of a parent category's subcategories (e.g. "Large appliances"
/// under "AC &amp; Appliance Repair"): CRUD, activation. Flat, top-level
/// route - same shape as <see cref="ServiceGroupsController"/> - because
/// category groups get their own admin-web tab rather than living only
/// under one category's edit page. Gated behind the "catalog" permission
/// module, same as <see cref="CategoriesController"/> (SRS 12.5-12.7 share
/// one module).
/// </summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/admin/catalog/category-groups")]
[Authorize(AuthenticationSchemes = DependencyInjection.AdminJwtBearerScheme)]
public class CategoryGroupsController : ControllerBase
{
    private const string ReadPolicy = AdminModules.Catalog + ".read";
    private const string WritePolicy = AdminModules.Catalog + ".write";

    private readonly ICategoryGroupManagementService _groupManagementService;
    private readonly IValidator<CategoryGroupCreateRequest> _createValidator;
    private readonly IValidator<CategoryGroupUpdateRequest> _updateValidator;

    public CategoryGroupsController(
        ICategoryGroupManagementService groupManagementService,
        IValidator<CategoryGroupCreateRequest> createValidator,
        IValidator<CategoryGroupUpdateRequest> updateValidator)
    {
        _groupManagementService = groupManagementService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    [HttpGet]
    [Authorize(Policy = ReadPolicy)]
    [ProducesResponseType(typeof(IReadOnlyList<CategoryGroupAdminResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] Guid? categoryId) =>
        Ok(await _groupManagementService.ListAsync(categoryId));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = ReadPolicy)]
    [ProducesResponseType(typeof(CategoryGroupAdminResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _groupManagementService.GetByIdAsync(id);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    [HttpPost]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(typeof(CategoryGroupAdminResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create([FromBody] CategoryGroupCreateRequest request)
    {
        var validation = await _createValidator.ValidateAsync(request);
        if (!validation.IsValid) return ValidationProblem(ToModelState(validation));

        var result = await _groupManagementService.CreateAsync(request);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(typeof(CategoryGroupAdminResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(Guid id, [FromBody] CategoryGroupUpdateRequest request)
    {
        var validation = await _updateValidator.ValidateAsync(request);
        if (!validation.IsValid) return ValidationProblem(ToModelState(validation));

        var result = await _groupManagementService.UpdateAsync(id, request);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    [HttpPost("{id:guid}/activate")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Activate(Guid id)
    {
        var result = await _groupManagementService.SetActiveAsync(id, true);
        return result.IsSuccess ? NoContent() : result.ToProblemResult();
    }

    [HttpPost("{id:guid}/deactivate")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        var result = await _groupManagementService.SetActiveAsync(id, false);
        return result.IsSuccess ? NoContent() : result.ToProblemResult();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _groupManagementService.DeleteAsync(id);
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

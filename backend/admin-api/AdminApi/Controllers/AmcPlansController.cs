using Asp.Versioning;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Nestly.Application.Amc;
using Nestly.BuildingBlocks.Extensions;
using Nestly.Domain;
using Nestly.Infrastructure;

namespace Nestly.AdminApi.Controllers;

/// <summary>
/// Admin CRUD for the AMC plan catalog (docs/AMC.md): category, price, term,
/// visits included. Mirrors <see cref="SubscriptionPlansController"/> exactly.
/// Read-only actions require "subscription.read"; mutating actions require
/// "subscription.write" - docs/AMC.md's RBAC ADDITIONS section: an AMC
/// contract is a commercial record adjacent to Subscription, not a new
/// vertical, so no new <c>AdminModules</c> entry is added.
/// </summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/admin/amc-plans")]
[Authorize(AuthenticationSchemes = DependencyInjection.AdminJwtBearerScheme)]
public class AmcPlansController : ControllerBase
{
    private const string ReadPolicy = AdminModules.Subscription + ".read";
    private const string WritePolicy = AdminModules.Subscription + ".write";

    private readonly IAmcAdminService _adminService;
    private readonly IValidator<AmcPlanCreateRequest> _createValidator;
    private readonly IValidator<AmcPlanUpdateRequest> _updateValidator;

    public AmcPlansController(
        IAmcAdminService adminService,
        IValidator<AmcPlanCreateRequest> createValidator,
        IValidator<AmcPlanUpdateRequest> updateValidator)
    {
        _adminService = adminService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    [HttpGet]
    [Authorize(Policy = ReadPolicy)]
    [ProducesResponseType(typeof(IReadOnlyList<AmcPlanAdminResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List() => Ok(await _adminService.ListAllPlansAsync());

    [HttpGet("{id:guid}")]
    [Authorize(Policy = ReadPolicy)]
    [ProducesResponseType(typeof(AmcPlanAdminResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _adminService.GetPlanByIdAsync(id);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    [HttpPost]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(typeof(AmcPlanAdminResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] AmcPlanCreateRequest request)
    {
        var validation = await _createValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _adminService.CreatePlanAsync(request);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value)
            : result.ToProblemResult();
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(typeof(AmcPlanAdminResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] AmcPlanUpdateRequest request)
    {
        var validation = await _updateValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _adminService.UpdatePlanAsync(id, request, CurrentAdminUserId());
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    [HttpPost("{id:guid}/activate")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Activate(Guid id)
    {
        var result = await _adminService.ActivatePlanAsync(id, CurrentAdminUserId());
        return result.IsSuccess ? NoContent() : result.ToProblemResult();
    }

    [HttpPost("{id:guid}/deactivate")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        var result = await _adminService.DeactivatePlanAsync(id, CurrentAdminUserId());
        return result.IsSuccess ? NoContent() : result.ToProblemResult();
    }

    private Guid CurrentAdminUserId() =>
        User.GetSubjectId();

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

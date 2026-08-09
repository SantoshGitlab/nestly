using Asp.Versioning;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Nestly.Application.AdminRoleManagement;
using Nestly.BuildingBlocks.Extensions;
using Nestly.Domain;
using Nestly.Infrastructure;

namespace Nestly.AdminApi.Controllers;

/// <summary>
/// Role CRUD and permission-matrix editing (SRS 12.2.2, 12.2.3, task 313):
/// AdminPermissionCatalog's nine seeded roles and their grants used to be
/// compile-time constants - changing who could do what required a code
/// change and redeploy. This controller makes <see cref="AdminRole"/> and its
/// permission grants genuinely writable at runtime. Gated behind
/// "settings.read"/"settings.write" - the same two policies
/// <see cref="AdminUsersController"/> already uses for admin-user
/// administration (nothing else in the seeded permission matrix grants
/// Settings besides Super Admin).
///
/// Every permission-granting write is subject to a self-escalation guard
/// (see <c>AdminRoleManagementService</c>'s doc comment) - a 403 from any
/// action below most likely means that guard rejected the request, not a
/// missing policy grant (the [Authorize] attribute would already have
/// produced the 403 for that).
/// </summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/admin/admin-roles")]
[Authorize(AuthenticationSchemes = DependencyInjection.AdminJwtBearerScheme)]
public class AdminRolesController : ControllerBase
{
    private const string SettingsReadPolicy = AdminModules.Settings + ".read";
    private const string SettingsWritePolicy = AdminModules.Settings + ".write";

    private readonly IAdminRoleManagementService _adminRoleManagementService;
    private readonly IValidator<CreateAdminRoleRequest> _createValidator;
    private readonly IValidator<UpdateAdminRoleRequest> _updateValidator;
    private readonly IValidator<SetAdminRolePermissionsRequest> _setPermissionsValidator;

    public AdminRolesController(
        IAdminRoleManagementService adminRoleManagementService,
        IValidator<CreateAdminRoleRequest> createValidator,
        IValidator<UpdateAdminRoleRequest> updateValidator,
        IValidator<SetAdminRolePermissionsRequest> setPermissionsValidator)
    {
        _adminRoleManagementService = adminRoleManagementService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _setPermissionsValidator = setPermissionsValidator;
    }

    /// <summary>Every grantable permission code (module x action), for the permission-matrix editor's grid.</summary>
    [HttpGet("permissions")]
    [Authorize(Policy = SettingsReadPolicy)]
    [ProducesResponseType(typeof(IReadOnlyList<AdminPermissionCatalogEntryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPermissionCatalog()
    {
        var result = await _adminRoleManagementService.GetPermissionCatalogAsync();
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Every role with its currently granted permission codes.</summary>
    [HttpGet]
    [Authorize(Policy = SettingsReadPolicy)]
    [ProducesResponseType(typeof(IReadOnlyList<AdminRoleDetailResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List()
    {
        var result = await _adminRoleManagementService.ListAsync();
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Role detail, including its current permission-matrix row.</summary>
    [HttpGet("{roleId:guid}")]
    [Authorize(Policy = SettingsReadPolicy)]
    [ProducesResponseType(typeof(AdminRoleDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid roleId)
    {
        var result = await _adminRoleManagementService.GetByIdAsync(roleId);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Creates a new role with an initial permission-matrix row (SRS 12.2.2 "roles are configurable").</summary>
    [HttpPost]
    [Authorize(Policy = SettingsWritePolicy)]
    [ProducesResponseType(typeof(AdminRoleDetailResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateAdminRoleRequest request)
    {
        var validation = await _createValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _adminRoleManagementService.CreateAsync(request, CurrentAdminUserId());
        if (!result.IsSuccess)
        {
            return result.ToProblemResult();
        }

        return CreatedAtAction(nameof(GetById), new { roleId = result.Value.Id }, result.Value);
    }

    /// <summary>Renames a role / edits its description - permissions are edited separately below.</summary>
    [HttpPut("{roleId:guid}")]
    [Authorize(Policy = SettingsWritePolicy)]
    [ProducesResponseType(typeof(AdminRoleDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid roleId, [FromBody] UpdateAdminRoleRequest request)
    {
        var validation = await _updateValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _adminRoleManagementService.UpdateAsync(roleId, request, CurrentAdminUserId());
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>
    /// Replaces a role's entire permission-matrix row (SRS 12.2.3) - a
    /// full-replace with the complete grid state, not an add/remove delta.
    /// Subject to the self-escalation guard: rejected with 403 if it would
    /// grant the role any code the caller does not already hold.
    /// </summary>
    [HttpPut("{roleId:guid}/permissions")]
    [Authorize(Policy = SettingsWritePolicy)]
    [ProducesResponseType(typeof(AdminRoleDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetPermissions(Guid roleId, [FromBody] SetAdminRolePermissionsRequest request)
    {
        var validation = await _setPermissionsValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _adminRoleManagementService.SetPermissionsAsync(roleId, request, CurrentAdminUserId());
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
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

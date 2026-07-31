using System.IdentityModel.Tokens.Jwt;
using Asp.Versioning;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Nestly.Application.AdminUserManagement;
using Nestly.BuildingBlocks.Extensions;
using Nestly.Domain;
using Nestly.Infrastructure;

namespace Nestly.AdminApi.Controllers;

/// <summary>
/// Admin user management (SRS 12.2.1, tasks 97a-97d): CRUD over admin
/// accounts, role assignment, activate/deactivate, and admin-initiated
/// password reset - one Super Admin managing another back-office operator's
/// account. Gated behind "settings.read"/"settings.write" - the same two
/// policies <see cref="AdminAuthController.Unlock"/> already uses for
/// administering another admin's account (nothing else in the seeded
/// permission matrix grants Settings besides Super Admin, per
/// <c>AdminPermissionCatalog</c>).
/// </summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/admin/admin-users")]
[Authorize(AuthenticationSchemes = DependencyInjection.AdminJwtBearerScheme)]
public class AdminUsersController : ControllerBase
{
    private const string SettingsReadPolicy = AdminModules.Settings + ".read";
    private const string SettingsWritePolicy = AdminModules.Settings + ".write";

    private readonly IAdminUserManagementService _adminUserManagementService;
    private readonly IValidator<AdminUserSearchRequest> _searchValidator;
    private readonly IValidator<CreateAdminUserRequest> _createValidator;
    private readonly IValidator<UpdateAdminUserRequest> _updateValidator;

    public AdminUsersController(
        IAdminUserManagementService adminUserManagementService,
        IValidator<AdminUserSearchRequest> searchValidator,
        IValidator<CreateAdminUserRequest> createValidator,
        IValidator<UpdateAdminUserRequest> updateValidator)
    {
        _adminUserManagementService = adminUserManagementService;
        _searchValidator = searchValidator;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    /// <summary>Search/filter admin accounts (task 97a "list").</summary>
    [HttpGet]
    [Authorize(Policy = SettingsReadPolicy)]
    [ProducesResponseType(typeof(AdminUserSearchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Search(
        [FromQuery] string? email,
        [FromQuery] string? name,
        [FromQuery] AdminUserStatus? status,
        [FromQuery] Guid? roleId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var request = new AdminUserSearchRequest(email, name, status, roleId, page, pageSize);

        var validation = await _searchValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _adminUserManagementService.SearchAsync(request);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Every seeded/created role, for the role-assignment picker (task 97b).</summary>
    [HttpGet("roles")]
    [Authorize(Policy = SettingsReadPolicy)]
    [ProducesResponseType(typeof(IReadOnlyList<AdminRoleSummaryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListRoles()
    {
        var result = await _adminUserManagementService.ListRolesAsync();
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Admin account detail (task 97a "get").</summary>
    [HttpGet("{adminUserId:guid}")]
    [Authorize(Policy = SettingsReadPolicy)]
    [ProducesResponseType(typeof(AdminUserDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid adminUserId)
    {
        var result = await _adminUserManagementService.GetByIdAsync(adminUserId);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Provisions a new admin account (SRS 12.2.1 "Create admin users", task 97a).</summary>
    [HttpPost]
    [Authorize(Policy = SettingsWritePolicy)]
    [ProducesResponseType(typeof(AdminUserDetailResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateAdminUserRequest request)
    {
        var validation = await _createValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _adminUserManagementService.CreateAsync(request, CurrentAdminUserId());
        if (!result.IsSuccess)
        {
            return result.ToProblemResult();
        }

        return CreatedAtAction(nameof(GetById), new { adminUserId = result.Value.Id }, result.Value);
    }

    /// <summary>Edits an admin account's profile - email and name (SRS 12.2.1 "Edit admin user profile", task 97a).</summary>
    [HttpPut("{adminUserId:guid}")]
    [Authorize(Policy = SettingsWritePolicy)]
    [ProducesResponseType(typeof(AdminUserDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid adminUserId, [FromBody] UpdateAdminUserRequest request)
    {
        var validation = await _updateValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _adminUserManagementService.UpdateAsync(adminUserId, request, CurrentAdminUserId());
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Assigns or clears an admin account's role (SRS 12.2.1 "Assign role(s)", task 97b).</summary>
    [HttpPut("{adminUserId:guid}/role")]
    [Authorize(Policy = SettingsWritePolicy)]
    [ProducesResponseType(typeof(AdminUserDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignRole(Guid adminUserId, [FromBody] AssignAdminRoleRequest request)
    {
        var result = await _adminUserManagementService.AssignRoleAsync(adminUserId, request, CurrentAdminUserId());
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Activates a deactivated admin account (SRS 12.2.1 "Activate/deactivate users", task 97c).</summary>
    [HttpPost("{adminUserId:guid}/activate")]
    [Authorize(Policy = SettingsWritePolicy)]
    [ProducesResponseType(typeof(AdminUserDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Activate(Guid adminUserId)
    {
        var result = await _adminUserManagementService.ActivateAsync(adminUserId, CurrentAdminUserId());
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>
    /// Deactivates an admin account (SRS 12.2.1 "Activate/deactivate users",
    /// task 97c) - distinct from clearing a login lockout
    /// (<see cref="AdminAuthController.Unlock"/>, task 95d): this permanently
    /// disables login until reactivated, rather than clearing a temporary,
    /// self-resolving failed-attempt lockout.
    /// </summary>
    [HttpPost("{adminUserId:guid}/deactivate")]
    [Authorize(Policy = SettingsWritePolicy)]
    [ProducesResponseType(typeof(AdminUserDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Deactivate(Guid adminUserId)
    {
        var result = await _adminUserManagementService.DeactivateAsync(adminUserId, CurrentAdminUserId());
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>
    /// Admin-initiated password reset (SRS 12.2.1 "Reset password / send
    /// reset link", task 97d): generates a temporary password and returns it
    /// once for the Super Admin to relay to the account owner out of band.
    /// </summary>
    [HttpPost("{adminUserId:guid}/reset-password")]
    [Authorize(Policy = SettingsWritePolicy)]
    [ProducesResponseType(typeof(ResetAdminPasswordResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetPassword(Guid adminUserId)
    {
        var result = await _adminUserManagementService.ResetPasswordAsync(adminUserId, CurrentAdminUserId());
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    private Guid CurrentAdminUserId() =>
        Guid.Parse(User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);

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

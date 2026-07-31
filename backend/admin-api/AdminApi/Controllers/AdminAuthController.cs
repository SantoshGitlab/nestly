using Asp.Versioning;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.RateLimiting;
using Nestly.Application.Identity;
using Nestly.BuildingBlocks.Extensions;

namespace Nestly.AdminApi.Controllers;

/// <summary>Admin panel authentication (SRS 12.1, tasks 95a-95g).</summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/admin/auth")]
public class AdminAuthController : ControllerBase
{
    private readonly IAdminLoginService _loginService;
    private readonly IValidator<AdminLoginRequest> _loginValidator;

    public AdminAuthController(IAdminLoginService loginService, IValidator<AdminLoginRequest> loginValidator)
    {
        _loginService = loginService;
        _loginValidator = loginValidator;
    }

    /// <summary>
    /// Admin login (SRS 12.1.1): email + password, JWT issuance, lockout and
    /// login audit. Throttled per-IP by the "login" rate-limit policy
    /// (task 95c); per-account throttling/lockout (95d) happens inside
    /// <see cref="IAdminLoginService"/> itself.
    /// </summary>
    [EnableRateLimiting("login")]
    [HttpPost("login")]
    [ProducesResponseType(typeof(AdminLoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Login([FromBody] AdminLoginRequest request)
    {
        var validation = await _loginValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _loginService.LoginAsync(request);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>
    /// Administrative unlock of a locked account (task 95d's unlock path). A
    /// lockout also clears itself automatically once its window elapses;
    /// this only clears it sooner.
    ///
    /// Deliberately not <c>[Authorize]</c>-gated yet - the same situation as
    /// <c>SupportTicketDisputesController</c>: admin authorization
    /// enforcement (role/permission checks) is tasks 96b/96c, not built yet.
    /// Phase 6 wires a Super-Admin-only policy in front of this action
    /// rather than this task rebuilding authorization early.
    /// </summary>
    [HttpPost("unlock/{adminUserId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Unlock(Guid adminUserId)
    {
        var result = await _loginService.UnlockAsync(adminUserId);
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

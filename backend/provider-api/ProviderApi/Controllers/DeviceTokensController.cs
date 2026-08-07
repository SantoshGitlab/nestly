using Asp.Versioning;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Nestly.Application.Notifications;
using Nestly.BuildingBlocks.Extensions;
using Nestly.Domain;
using Nestly.Infrastructure;

namespace Nestly.ProviderApi.Controllers;

/// <summary>
/// Push device token registration for providers (task 277), mirroring
/// consumer-api's <c>DeviceTokensController</c> field for field - the only
/// difference is the scheme and that the caller's id becomes a
/// <see cref="DeviceTokenOwner"/> provider, not a customer.
/// </summary>
[ApiController]
[ApiVersion(1)]
[Authorize(AuthenticationSchemes = DependencyInjection.ProviderJwtBearerScheme)]
[Route("api/v{version:apiVersion}/device-tokens")]
public class DeviceTokensController : ControllerBase
{
    private readonly IDeviceTokenService _deviceTokenService;
    private readonly IValidator<RegisterDeviceTokenRequest> _registerValidator;

    public DeviceTokensController(IDeviceTokenService deviceTokenService, IValidator<RegisterDeviceTokenRequest> registerValidator)
    {
        _deviceTokenService = deviceTokenService;
        _registerValidator = registerValidator;
    }

    /// <summary>Registers (or re-registers) the caller's device for push notifications.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(DeviceTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterDeviceTokenRequest request)
    {
        var validation = await _registerValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _deviceTokenService.RegisterAsync(CurrentOwner(), request);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>The caller's active registered devices.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<DeviceTokenResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List()
    {
        var result = await _deviceTokenService.ListAsync(CurrentOwner());
        return Ok(result.Value);
    }

    /// <summary>Deactivates a device (e.g. on logout).</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Revoke(Guid id)
    {
        var result = await _deviceTokenService.RevokeAsync(CurrentOwner(), id);
        return result.IsSuccess ? NoContent() : result.ToProblemResult();
    }

    private DeviceTokenOwner CurrentOwner() =>
        DeviceTokenOwner.ForProvider(User.GetSubjectId());

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

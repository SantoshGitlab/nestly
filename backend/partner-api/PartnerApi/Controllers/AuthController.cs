using Asp.Versioning;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.RateLimiting;
using Nestly.Application.PartnerIdentity;
using Nestly.BuildingBlocks.Extensions;

namespace Nestly.PartnerApi.Controllers;

/// <summary>
/// Partner authentication (task 146a/146b, PARTNER.md API surface "Auth").
/// OTP-only — there is no password login for partners, so this is
/// structurally simpler than consumer-api's <c>AuthController</c>, which it
/// otherwise mirrors.
/// </summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/auth")]
public class AuthController : ControllerBase
{
    private readonly IPartnerRegistrationService _registrationService;
    private readonly IPartnerLoginService _loginService;
    private readonly IValidator<RequestPartnerRegistrationOtpRequest> _registrationOtpValidator;
    private readonly IValidator<RegisterPartnerRequest> _registerValidator;
    private readonly IValidator<RequestPartnerLoginOtpRequest> _loginOtpValidator;
    private readonly IValidator<LoginPartnerWithOtpRequest> _loginWithOtpValidator;
    private readonly IValidator<RefreshPartnerTokenRequest> _refreshValidator;
    private readonly IValidator<LogoutPartnerRequest> _logoutValidator;

    public AuthController(
        IPartnerRegistrationService registrationService,
        IPartnerLoginService loginService,
        IValidator<RequestPartnerRegistrationOtpRequest> registrationOtpValidator,
        IValidator<RegisterPartnerRequest> registerValidator,
        IValidator<RequestPartnerLoginOtpRequest> loginOtpValidator,
        IValidator<LoginPartnerWithOtpRequest> loginWithOtpValidator,
        IValidator<RefreshPartnerTokenRequest> refreshValidator,
        IValidator<LogoutPartnerRequest> logoutValidator)
    {
        _registrationService = registrationService;
        _loginService = loginService;
        _registrationOtpValidator = registrationOtpValidator;
        _registerValidator = registerValidator;
        _loginOtpValidator = loginOtpValidator;
        _loginWithOtpValidator = loginWithOtpValidator;
        _refreshValidator = refreshValidator;
        _logoutValidator = logoutValidator;
    }

    /// <summary>Step 1: send a registration OTP to a mobile number.</summary>
    [EnableRateLimiting("otp")]
    [HttpPost("registration/otp")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RequestRegistrationOtp([FromBody] RequestPartnerRegistrationOtpRequest request)
    {
        var validation = await _registrationOtpValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _registrationService.RequestOtpAsync(request);
        return result.IsSuccess ? NoContent() : result.ToProblemResult();
    }

    /// <summary>Step 2: complete registration once the OTP has been verified.</summary>
    [HttpPost("registration")]
    [ProducesResponseType(typeof(PartnerSummaryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterPartnerRequest request)
    {
        var validation = await _registerValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _registrationService.RegisterAsync(request);
        return result.IsSuccess ? StatusCode(StatusCodes.Status201Created, result.Value) : result.ToProblemResult();
    }

    /// <summary>Send a login OTP to an already-registered mobile number.</summary>
    [EnableRateLimiting("otp")]
    [HttpPost("login/otp")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RequestLoginOtp([FromBody] RequestPartnerLoginOtpRequest request)
    {
        var validation = await _loginOtpValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _loginService.RequestOtpAsync(request);
        return result.IsSuccess ? NoContent() : result.ToProblemResult();
    }

    /// <summary>Login via mobile OTP.</summary>
    [EnableRateLimiting("login")]
    [HttpPost("login/otp/verify")]
    [ProducesResponseType(typeof(PartnerLoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> LoginWithOtp([FromBody] LoginPartnerWithOtpRequest request)
    {
        var validation = await _loginWithOtpValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _loginService.LoginWithOtpAsync(request);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Exchange a still-valid refresh token for a new access+refresh pair (rotation).</summary>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(PartnerLoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] RefreshPartnerTokenRequest request)
    {
        var validation = await _refreshValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _loginService.RefreshAsync(request);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Invalidate a session's refresh token.</summary>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Logout([FromBody] LogoutPartnerRequest request)
    {
        var validation = await _logoutValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _loginService.LogoutAsync(request);
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

using Asp.Versioning;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.RateLimiting;
using Nestly.Application.Identity;
using Nestly.BuildingBlocks.Extensions;

namespace Nestly.ConsumerApi.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/auth")]
public class AuthController : ControllerBase
{
    private readonly ICustomerRegistrationService _registrationService;
    private readonly ICustomerLoginService _loginService;
    private readonly IValidator<RequestRegistrationOtpRequest> _otpRequestValidator;
    private readonly IValidator<RegisterCustomerRequest> _registerValidator;
    private readonly IValidator<RequestLoginOtpRequest> _loginOtpRequestValidator;
    private readonly IValidator<LoginWithOtpRequest> _loginWithOtpValidator;
    private readonly IValidator<LoginWithPasswordRequest> _loginWithPasswordValidator;
    private readonly IValidator<RefreshTokenRequest> _refreshValidator;
    private readonly IValidator<LogoutRequest> _logoutValidator;
    private readonly ICustomerPasswordResetService _passwordResetService;
    private readonly IValidator<ForgotPasswordRequest> _forgotPasswordValidator;
    private readonly IValidator<ResetPasswordRequest> _resetPasswordValidator;

    public AuthController(
        ICustomerRegistrationService registrationService,
        ICustomerLoginService loginService,
        ICustomerPasswordResetService passwordResetService,
        IValidator<RequestRegistrationOtpRequest> otpRequestValidator,
        IValidator<RegisterCustomerRequest> registerValidator,
        IValidator<RequestLoginOtpRequest> loginOtpRequestValidator,
        IValidator<LoginWithOtpRequest> loginWithOtpValidator,
        IValidator<LoginWithPasswordRequest> loginWithPasswordValidator,
        IValidator<RefreshTokenRequest> refreshValidator,
        IValidator<LogoutRequest> logoutValidator,
        IValidator<ForgotPasswordRequest> forgotPasswordValidator,
        IValidator<ResetPasswordRequest> resetPasswordValidator)
    {
        _registrationService = registrationService;
        _loginService = loginService;
        _passwordResetService = passwordResetService;
        _forgotPasswordValidator = forgotPasswordValidator;
        _resetPasswordValidator = resetPasswordValidator;
        _otpRequestValidator = otpRequestValidator;
        _registerValidator = registerValidator;
        _loginOtpRequestValidator = loginOtpRequestValidator;
        _loginWithOtpValidator = loginWithOtpValidator;
        _loginWithPasswordValidator = loginWithPasswordValidator;
        _refreshValidator = refreshValidator;
        _logoutValidator = logoutValidator;
    }

    /// <summary>Step 1: send a registration OTP to a mobile number (SRS 11.2.1).</summary>
    [EnableRateLimiting("otp")]
    [HttpPost("registration/otp")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RequestRegistrationOtp([FromBody] RequestRegistrationOtpRequest request)
    {
        var validation = await _otpRequestValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _registrationService.RequestOtpAsync(request);
        return result.IsSuccess ? Ok() : result.ToProblemResult();
    }

    /// <summary>Step 2: complete registration once the OTP has been verified (SRS 11.2.1).</summary>
    [HttpPost("registration")]
    [ProducesResponseType(typeof(CustomerSummaryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterCustomerRequest request)
    {
        var validation = await _registerValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _registrationService.RegisterAsync(request);
        return result.IsSuccess ? StatusCode(StatusCodes.Status201Created, result.Value) : result.ToProblemResult();
    }

    /// <summary>Send a login OTP to an already-registered mobile number (SRS 11.2.2).</summary>
    [EnableRateLimiting("otp")]
    [HttpPost("login/otp")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RequestLoginOtp([FromBody] RequestLoginOtpRequest request)
    {
        var validation = await _loginOtpRequestValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _loginService.RequestOtpAsync(request);
        return result.IsSuccess ? Ok() : result.ToProblemResult();
    }

    /// <summary>Login via mobile OTP (SRS 11.2.2).</summary>
    [EnableRateLimiting("login")]
    [HttpPost("login/otp/verify")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> LoginWithOtp([FromBody] LoginWithOtpRequest request)
    {
        var validation = await _loginWithOtpValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _loginService.LoginWithOtpAsync(request);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Login via email + password, when password auth is enabled (SRS 11.2.2).</summary>
    [EnableRateLimiting("login")]
    [HttpPost("login/password")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> LoginWithPassword([FromBody] LoginWithPasswordRequest request)
    {
        var validation = await _loginWithPasswordValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _loginService.LoginWithPasswordAsync(request);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Exchange a still-valid refresh token for a new access+refresh pair (rotation, SRS 28.3).</summary>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request)
    {
        var validation = await _refreshValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _loginService.RefreshAsync(request);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Invalidate a session's refresh token (SRS 11.2.2: logout invalidates the active session).</summary>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request)
    {
        var validation = await _logoutValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _loginService.LogoutAsync(request);
        return result.IsSuccess ? NoContent() : result.ToProblemResult();
    }

    /// <summary>
    /// Step 1 of the reset flow (SRS 11.2.2). Always 200 — see
    /// <see cref="ICustomerPasswordResetService.RequestResetAsync"/> for why
    /// an unknown address is not reported as such.
    /// </summary>
    [EnableRateLimiting("otp")]
    [HttpPost("password/forgot")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        var validation = await _forgotPasswordValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _passwordResetService.RequestResetAsync(request);
        return result.IsSuccess ? Ok() : result.ToProblemResult();
    }

    /// <summary>Step 2: set the new password once the OTP verifies (SRS 11.2.2).</summary>
    [EnableRateLimiting("login")]
    [HttpPost("password/reset")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        var validation = await _resetPasswordValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _passwordResetService.ResetAsync(request);
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

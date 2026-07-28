using Asp.Versioning;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Nestly.Application.Identity;
using Nestly.BuildingBlocks.Extensions;

namespace Nestly.ConsumerApi.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/auth")]
public class AuthController : ControllerBase
{
    private readonly ICustomerRegistrationService _registrationService;
    private readonly IValidator<RequestRegistrationOtpRequest> _otpRequestValidator;
    private readonly IValidator<RegisterCustomerRequest> _registerValidator;

    public AuthController(
        ICustomerRegistrationService registrationService,
        IValidator<RequestRegistrationOtpRequest> otpRequestValidator,
        IValidator<RegisterCustomerRequest> registerValidator)
    {
        _registrationService = registrationService;
        _otpRequestValidator = otpRequestValidator;
        _registerValidator = registerValidator;
    }

    /// <summary>Step 1: send a registration OTP to a mobile number (SRS 11.2.1).</summary>
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

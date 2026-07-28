using System.IdentityModel.Tokens.Jwt;
using Asp.Versioning;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.RateLimiting;
using Nestly.Application.Profile;
using Nestly.BuildingBlocks.Extensions;

namespace Nestly.ConsumerApi.Controllers;

/// <summary>
/// Customer profile (SRS 11.2.3). Like the address book, every action is
/// scoped to the caller's own customer id taken from the JWT — there is no
/// route or body parameter that could name a different customer.
/// </summary>
[ApiController]
[ApiVersion(1)]
[Authorize]
[Route("api/v{version:apiVersion}/profile")]
public class CustomerProfileController : ControllerBase
{
    private readonly ICustomerProfileService _profileService;
    private readonly IValidator<UpdateProfileRequest> _updateValidator;
    private readonly IValidator<RequestMobileChangeOtpRequest> _mobileOtpValidator;
    private readonly IValidator<ConfirmMobileChangeRequest> _mobileConfirmValidator;
    private readonly IValidator<RequestEmailChangeOtpRequest> _emailOtpValidator;
    private readonly IValidator<ConfirmEmailChangeRequest> _emailConfirmValidator;

    public CustomerProfileController(
        ICustomerProfileService profileService,
        IValidator<UpdateProfileRequest> updateValidator,
        IValidator<RequestMobileChangeOtpRequest> mobileOtpValidator,
        IValidator<ConfirmMobileChangeRequest> mobileConfirmValidator,
        IValidator<RequestEmailChangeOtpRequest> emailOtpValidator,
        IValidator<ConfirmEmailChangeRequest> emailConfirmValidator)
    {
        _profileService = profileService;
        _updateValidator = updateValidator;
        _mobileOtpValidator = mobileOtpValidator;
        _mobileConfirmValidator = mobileConfirmValidator;
        _emailOtpValidator = emailOtpValidator;
        _emailConfirmValidator = emailConfirmValidator;
    }

    /// <summary>View profile (SRS 11.2.3).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(CustomerProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get()
    {
        var result = await _profileService.GetAsync(CurrentCustomerId());
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Edit name and optional profile data (SRS 11.2.3). Mobile/email change through the endpoints below.</summary>
    [HttpPut]
    [ProducesResponseType(typeof(CustomerProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update([FromBody] UpdateProfileRequest request)
    {
        var validation = await _updateValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _profileService.UpdateAsync(CurrentCustomerId(), request);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Step 1 of a mobile change: send a code to the new number (SRS 11.2.3).</summary>
    [EnableRateLimiting("otp")]
    [HttpPost("mobile/otp")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RequestMobileChangeOtp([FromBody] RequestMobileChangeOtpRequest request)
    {
        var validation = await _mobileOtpValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _profileService.RequestMobileChangeOtpAsync(CurrentCustomerId(), request);
        return result.IsSuccess ? Ok() : result.ToProblemResult();
    }

    /// <summary>Step 2: apply the mobile change once the code verifies (SRS 11.2.3).</summary>
    [HttpPost("mobile")]
    [ProducesResponseType(typeof(CustomerProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ConfirmMobileChange([FromBody] ConfirmMobileChangeRequest request)
    {
        var validation = await _mobileConfirmValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _profileService.ConfirmMobileChangeAsync(CurrentCustomerId(), request);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Step 1 of an email change: send a code to the new address (SRS 11.2.3).</summary>
    [EnableRateLimiting("otp")]
    [HttpPost("email/otp")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RequestEmailChangeOtp([FromBody] RequestEmailChangeOtpRequest request)
    {
        var validation = await _emailOtpValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _profileService.RequestEmailChangeOtpAsync(CurrentCustomerId(), request);
        return result.IsSuccess ? Ok() : result.ToProblemResult();
    }

    /// <summary>Step 2: apply the email change once the code verifies (SRS 11.2.3).</summary>
    [HttpPost("email")]
    [ProducesResponseType(typeof(CustomerProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ConfirmEmailChange([FromBody] ConfirmEmailChangeRequest request)
    {
        var validation = await _emailConfirmValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _profileService.ConfirmEmailChangeAsync(CurrentCustomerId(), request);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Read communication preferences (SRS 11.2.3).</summary>
    [HttpGet("preferences")]
    [ProducesResponseType(typeof(CommunicationPreferencesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPreferences()
    {
        var result = await _profileService.GetPreferencesAsync(CurrentCustomerId());
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Replace communication preferences (SRS 11.2.3).</summary>
    [HttpPut("preferences")]
    [ProducesResponseType(typeof(CommunicationPreferencesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdatePreferences([FromBody] CommunicationPreferencesRequest request)
    {
        var result = await _profileService.UpdatePreferencesAsync(CurrentCustomerId(), request);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    private Guid CurrentCustomerId() =>
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

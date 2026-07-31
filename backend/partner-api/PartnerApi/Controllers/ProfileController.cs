using System.IdentityModel.Tokens.Jwt;
using Asp.Versioning;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Nestly.Application.PartnerIdentity;
using Nestly.Application.PartnerProfile;
using Nestly.BuildingBlocks.Extensions;
using Nestly.Infrastructure;

namespace Nestly.PartnerApi.Controllers;

/// <summary>
/// Partner profile, KYC, service areas and skills (task 149a, PARTNER.md API
/// surface "Profile/Onboarding"). Every action is scoped to the caller's own
/// partner id taken from the JWT — there is no route or body parameter that
/// could name a different partner (SRS 28.3 IDOR), mirroring
/// consumer-api's <c>CustomerProfileController</c>/<c>CustomerAddressController</c>.
/// </summary>
[ApiController]
[ApiVersion(1)]
[Authorize(AuthenticationSchemes = DependencyInjection.PartnerJwtBearerScheme)]
[Route("api/v{version:apiVersion}/profile")]
public class ProfileController : ControllerBase
{
    private readonly IPartnerProfileService _profileService;
    private readonly IPartnerKycService _kycService;
    private readonly IValidator<UpdatePartnerProfileRequest> _updateProfileValidator;
    private readonly IValidator<SubmitPartnerKycDocumentRequest> _kycDocumentValidator;
    private readonly IValidator<UpdatePartnerServiceAreasRequest> _serviceAreasValidator;
    private readonly IValidator<UpdatePartnerSkillsRequest> _skillsValidator;

    public ProfileController(
        IPartnerProfileService profileService,
        IPartnerKycService kycService,
        IValidator<UpdatePartnerProfileRequest> updateProfileValidator,
        IValidator<SubmitPartnerKycDocumentRequest> kycDocumentValidator,
        IValidator<UpdatePartnerServiceAreasRequest> serviceAreasValidator,
        IValidator<UpdatePartnerSkillsRequest> skillsValidator)
    {
        _profileService = profileService;
        _kycService = kycService;
        _updateProfileValidator = updateProfileValidator;
        _kycDocumentValidator = kycDocumentValidator;
        _serviceAreasValidator = serviceAreasValidator;
        _skillsValidator = skillsValidator;
    }

    /// <summary>View profile.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PartnerProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get()
    {
        var result = await _profileService.GetAsync(CurrentPartnerId());
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Edit legal name, display name and email.</summary>
    [HttpPut]
    [ProducesResponseType(typeof(PartnerProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update([FromBody] UpdatePartnerProfileRequest request)
    {
        var validation = await _updateProfileValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _profileService.UpdateAsync(CurrentPartnerId(), request);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Overall KYC picture: onboarding status plus every submitted document.</summary>
    [HttpGet("kyc")]
    [ProducesResponseType(typeof(PartnerKycStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetKycStatus()
    {
        var result = await _kycService.GetStatusAsync(CurrentPartnerId());
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>
    /// Submit a KYC document. <c>FileRef</c> is a reference to an
    /// already-uploaded file (storage key/URL) — this endpoint does not
    /// itself accept a binary upload, matching <see cref="IPartnerKycService"/>.
    /// </summary>
    [HttpPost("kyc/documents")]
    [ProducesResponseType(typeof(PartnerKycDocumentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SubmitKycDocument([FromBody] SubmitPartnerKycDocumentBody body)
    {
        var request = new SubmitPartnerKycDocumentRequest(CurrentPartnerId(), body.DocType, body.FileRef, body.DocNumber);
        var validation = await _kycDocumentValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _kycService.SubmitDocumentAsync(request);
        return result.IsSuccess ? StatusCode(StatusCodes.Status201Created, result.Value) : result.ToProblemResult();
    }

    /// <summary>List the partner's declared geography coverage.</summary>
    [HttpGet("service-areas")]
    [ProducesResponseType(typeof(IReadOnlyList<PartnerServiceAreaResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetServiceAreas()
    {
        return Ok(await _profileService.GetServiceAreasAsync(CurrentPartnerId()));
    }

    /// <summary>Replace the partner's whole geography coverage set.</summary>
    [HttpPut("service-areas")]
    [ProducesResponseType(typeof(IReadOnlyList<PartnerServiceAreaResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateServiceAreas([FromBody] UpdatePartnerServiceAreasRequest request)
    {
        var validation = await _serviceAreasValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _profileService.UpdateServiceAreasAsync(CurrentPartnerId(), request);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>List the categories/services the partner is qualified for.</summary>
    [HttpGet("skills")]
    [ProducesResponseType(typeof(IReadOnlyList<PartnerSkillResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSkills()
    {
        return Ok(await _profileService.GetSkillsAsync(CurrentPartnerId()));
    }

    /// <summary>Replace the partner's whole declared skill set.</summary>
    [HttpPut("skills")]
    [ProducesResponseType(typeof(IReadOnlyList<PartnerSkillResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateSkills([FromBody] UpdatePartnerSkillsRequest request)
    {
        var validation = await _skillsValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _profileService.UpdateSkillsAsync(CurrentPartnerId(), request);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    private Guid CurrentPartnerId() =>
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

/// <summary>
/// Request body for <see cref="ProfileController.SubmitKycDocument"/> — the
/// partner id is deliberately excluded here (unlike
/// <see cref="SubmitPartnerKycDocumentRequest"/>) and taken from the JWT
/// instead, so a caller can never submit a document against another
/// partner's id (SRS 28.3 IDOR).
/// </summary>
public record SubmitPartnerKycDocumentBody(Nestly.Domain.PartnerKycDocumentType DocType, string FileRef, string? DocNumber);

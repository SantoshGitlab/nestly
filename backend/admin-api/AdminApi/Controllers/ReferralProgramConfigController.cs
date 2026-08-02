using Asp.Versioning;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.IdentityModel.Tokens.Jwt;
using Nestly.Application.Referral;
using Nestly.BuildingBlocks.Extensions;
using Nestly.Domain;
using Nestly.Infrastructure;

namespace Nestly.AdminApi.Controllers;

/// <summary>
/// Admin CRUD for the referral program config (reward types/values per side,
/// min qualifying order amount, expiry days, per-customer cap, active
/// window) plus task 174's milestone tiers (task 167). Read-only actions
/// require "referral.read"; every mutating action requires "referral.write" -
/// same per-action split as <see cref="CouponsController"/>.
/// </summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/admin/referral/config")]
[Authorize(AuthenticationSchemes = DependencyInjection.AdminJwtBearerScheme)]
public class ReferralProgramConfigController : ControllerBase
{
    private const string ReadPolicy = AdminModules.Referral + ".read";
    private const string WritePolicy = AdminModules.Referral + ".write";

    private readonly IReferralProgramConfigAdminService _configAdminService;
    private readonly IValidator<ReferralProgramConfigUpdateRequest> _updateValidator;
    private readonly IValidator<ReferralMilestoneCreateRequest> _milestoneCreateValidator;

    public ReferralProgramConfigController(
        IReferralProgramConfigAdminService configAdminService,
        IValidator<ReferralProgramConfigUpdateRequest> updateValidator,
        IValidator<ReferralMilestoneCreateRequest> milestoneCreateValidator)
    {
        _configAdminService = configAdminService;
        _updateValidator = updateValidator;
        _milestoneCreateValidator = milestoneCreateValidator;
    }

    /// <summary>The single referral program config row (task 167).</summary>
    [HttpGet]
    [Authorize(Policy = ReadPolicy)]
    [ProducesResponseType(typeof(ReferralProgramConfigResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get()
    {
        var result = await _configAdminService.GetAsync();
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Edits every mutable field of the referral program config (task 167).</summary>
    [HttpPut]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(typeof(ReferralProgramConfigResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update([FromBody] ReferralProgramConfigUpdateRequest request)
    {
        var validation = await _updateValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _configAdminService.UpdateAsync(request, CurrentAdminUserId());
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>All milestone tiers, active and inactive, ascending by threshold (task 174).</summary>
    [HttpGet("milestones")]
    [Authorize(Policy = ReadPolicy)]
    [ProducesResponseType(typeof(IReadOnlyList<ReferralMilestoneResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListMilestones() => Ok(await _configAdminService.ListMilestonesAsync());

    /// <summary>Creates a new milestone tier (task 174).</summary>
    [HttpPost("milestones")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(typeof(ReferralMilestoneResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateMilestone([FromBody] ReferralMilestoneCreateRequest request)
    {
        var validation = await _milestoneCreateValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _configAdminService.CreateMilestoneAsync(request);
        return result.IsSuccess
            ? CreatedAtAction(nameof(ListMilestones), null, result.Value)
            : result.ToProblemResult();
    }

    /// <summary>Re-enables a suspended milestone tier.</summary>
    [HttpPost("milestones/{milestoneId:guid}/activate")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(typeof(ReferralMilestoneResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ActivateMilestone(Guid milestoneId)
    {
        var result = await _configAdminService.SetMilestoneActiveAsync(milestoneId, true);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Suspends a milestone tier without deleting it.</summary>
    [HttpPost("milestones/{milestoneId:guid}/deactivate")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(typeof(ReferralMilestoneResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivateMilestone(Guid milestoneId)
    {
        var result = await _configAdminService.SetMilestoneActiveAsync(milestoneId, false);
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

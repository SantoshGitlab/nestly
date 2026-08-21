using Asp.Versioning;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Nestly.Application.ProviderReferral;
using Nestly.BuildingBlocks.Extensions;
using Nestly.Domain;
using Nestly.Infrastructure;

namespace Nestly.AdminApi.Controllers;

/// <summary>
/// Admin CRUD for the provider referral program config (reward values,
/// qualifying completed-job count, expiry days, per-provider cap, active
/// flag), mirrors <see cref="ReferralProgramConfigController"/>. Read-only
/// actions require "provider-referral.read"; every mutating action requires
/// "provider-referral.write".
/// </summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/admin/provider-referral/config")]
[Authorize(AuthenticationSchemes = DependencyInjection.AdminJwtBearerScheme)]
public class ProviderReferralProgramConfigController : ControllerBase
{
    private const string ReadPolicy = AdminModules.ProviderReferral + ".read";
    private const string WritePolicy = AdminModules.ProviderReferral + ".write";

    private readonly IProviderReferralProgramConfigAdminService _configAdminService;
    private readonly IValidator<ProviderReferralProgramConfigUpdateRequest> _updateValidator;

    public ProviderReferralProgramConfigController(
        IProviderReferralProgramConfigAdminService configAdminService,
        IValidator<ProviderReferralProgramConfigUpdateRequest> updateValidator)
    {
        _configAdminService = configAdminService;
        _updateValidator = updateValidator;
    }

    /// <summary>The single provider referral program config row.</summary>
    [HttpGet]
    [Authorize(Policy = ReadPolicy)]
    [ProducesResponseType(typeof(ProviderReferralProgramConfigResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get()
    {
        var result = await _configAdminService.GetAsync();
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Edits every mutable field of the provider referral program config.</summary>
    [HttpPut]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(typeof(ProviderReferralProgramConfigResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update([FromBody] ProviderReferralProgramConfigUpdateRequest request)
    {
        var validation = await _updateValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _configAdminService.UpdateAsync(request, CurrentAdminUserId());
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

using System.IdentityModel.Tokens.Jwt;
using Asp.Versioning;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Nestly.Application.NestlyCoins;
using Nestly.BuildingBlocks.Extensions;
using Nestly.Domain;
using Nestly.Domain.NestlyCoins;
using Nestly.Infrastructure;

namespace Nestly.AdminApi.Controllers;

/// <summary>
/// Admin get/update for the per-audience Nestly Coins program config, plus
/// the coins-issued/clawed-back report (docs/NESTLY-COINS.md API SURFACE,
/// task 202). Read-only actions require "nestly-coins.read"; mutating
/// actions require "nestly-coins.write" - same per-action split as
/// <see cref="ReferralProgramConfigController"/>.
/// </summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/admin/nestly-coins")]
[Authorize(AuthenticationSchemes = DependencyInjection.AdminJwtBearerScheme)]
public class AdminNestlyCoinsController : ControllerBase
{
    private const string ReadPolicy = AdminModules.NestlyCoins + ".read";
    private const string WritePolicy = AdminModules.NestlyCoins + ".write";

    private readonly INestlyCoinsAdminService _adminService;
    private readonly IValidator<NestlyCoinsProgramConfigUpsertRequest> _upsertValidator;

    public AdminNestlyCoinsController(
        INestlyCoinsAdminService adminService,
        IValidator<NestlyCoinsProgramConfigUpsertRequest> upsertValidator)
    {
        _adminService = adminService;
        _upsertValidator = upsertValidator;
    }

    /// <summary>The program config for one audience (task 200/202) - 404 if that audience has never been configured.</summary>
    [HttpGet("config/{audience}")]
    [Authorize(Policy = ReadPolicy)]
    [ProducesResponseType(typeof(NestlyCoinsProgramConfigResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetConfig(NestlyCoinsAudience audience)
    {
        var result = await _adminService.GetAsync(audience);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Creates or updates the program config for one audience - this is the only way an audience's coins program is ever activated.</summary>
    [HttpPut("config/{audience}")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(typeof(NestlyCoinsProgramConfigResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpsertConfig(NestlyCoinsAudience audience, [FromBody] NestlyCoinsProgramConfigUpsertRequest request)
    {
        var validation = await _upsertValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _adminService.UpsertAsync(audience, request, CurrentAdminUserId());
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Coins issued vs. clawed back for one audience over a date range (mirrors Referral's funnel/cost report).</summary>
    [HttpGet("reports/issued")]
    [Authorize(Policy = ReadPolicy)]
    [ProducesResponseType(typeof(NestlyCoinsReportResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReport(
        [FromQuery] NestlyCoinsAudience audience, [FromQuery] DateTime fromUtc, [FromQuery] DateTime toUtc)
    {
        var report = await _adminService.GetReportAsync(audience, fromUtc, toUtc);
        return Ok(report);
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

using System.IdentityModel.Tokens.Jwt;
using Asp.Versioning;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Nestly.Application.Referral;
using Nestly.BuildingBlocks.Extensions;
using Nestly.Domain;
using Nestly.Infrastructure;

namespace Nestly.AdminApi.Controllers;

/// <summary>
/// Admin referral list/detail, fraud review queue (task 166's
/// <see cref="IReferralFraudReviewService"/>, wired up here for the first
/// time), and funnel/cost reports (task 170, 171). Read-only actions require
/// "referral.read"; fraud-review actions and require "referral.write" - same
/// per-action split as <see cref="CouponsController"/> (this module collapses
/// REFERRAL.md's four permission tiers to the existing two, see
/// <see cref="AdminModules.Referral"/>'s doc comment).
/// </summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/admin/referral")]
[Authorize(AuthenticationSchemes = DependencyInjection.AdminJwtBearerScheme)]
public class ReferralsController : ControllerBase
{
    private const string ReadPolicy = AdminModules.Referral + ".read";
    private const string WritePolicy = AdminModules.Referral + ".write";

    private readonly IReferralAdminService _referralAdminService;
    private readonly IReferralFraudReviewService _fraudReviewService;
    private readonly IValidator<ReferralAdminSearchRequest> _searchValidator;

    public ReferralsController(
        IReferralAdminService referralAdminService,
        IReferralFraudReviewService fraudReviewService,
        IValidator<ReferralAdminSearchRequest> searchValidator)
    {
        _referralAdminService = referralAdminService;
        _fraudReviewService = fraudReviewService;
        _searchValidator = searchValidator;
    }

    /// <summary>Filter by status, fraud flag, and/or search by customer (task 170).</summary>
    [HttpGet]
    [Authorize(Policy = ReadPolicy)]
    [ProducesResponseType(typeof(ReferralAdminSearchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Search(
        [FromQuery] ReferralStatus? status,
        [FromQuery] bool? isFraudFlagged,
        [FromQuery] string? customerSearch,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var request = new ReferralAdminSearchRequest(status, isFraudFlagged, customerSearch, page, pageSize);

        var validation = await _searchValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        return Ok(await _referralAdminService.SearchAsync(request));
    }

    /// <summary>Only referrals currently flagged for fraud review - the fraud review queue (task 166, 170).</summary>
    [HttpGet("fraud-queue")]
    [Authorize(Policy = ReadPolicy)]
    [ProducesResponseType(typeof(ReferralAdminSearchResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> FraudQueue([FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
        Ok(await _referralAdminService.SearchAsync(new ReferralAdminSearchRequest(null, true, null, page, pageSize)));

    /// <summary>Referral detail view (task 170).</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = ReadPolicy)]
    [ProducesResponseType(typeof(ReferralAdminDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _referralAdminService.GetByIdAsync(id);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Manually flags a referral for fraud review (task 166).</summary>
    [HttpPost("{id:guid}/flag")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Flag(Guid id, [FromBody] ReferralFraudReviewRequest? request)
    {
        var result = await _fraudReviewService.FlagAsync(id, CurrentAdminUserId(), request?.Note);
        return result.IsSuccess ? NoContent() : result.ToProblemResult();
    }

    /// <summary>Confirms a flagged referral as a real abuse pattern (task 166) - the flag clears; any reward reversal is a separate, deliberate action.</summary>
    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ReferralFraudReviewRequest? request)
    {
        var result = await _fraudReviewService.ApproveAsync(id, CurrentAdminUserId(), request?.Note);
        return result.IsSuccess ? NoContent() : result.ToProblemResult();
    }

    /// <summary>Rejects a flag as a false positive (task 166) - the flag clears, no further action.</summary>
    [HttpPost("{id:guid}/reject")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] ReferralFraudReviewRequest? request)
    {
        var result = await _fraudReviewService.RejectAsync(id, CurrentAdminUserId(), request?.Note);
        return result.IsSuccess ? NoContent() : result.ToProblemResult();
    }

    /// <summary>Funnel report: invited/registered/qualified/rewarded, cohort-based over an optional date range (task 171).</summary>
    [HttpGet("reports/funnel")]
    [Authorize(Policy = ReadPolicy)]
    [ProducesResponseType(typeof(ReferralFunnelReportResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> FunnelReport([FromQuery] DateTime? fromUtc, [FromQuery] DateTime? toUtc)
    {
        var result = await _referralAdminService.GetFunnelReportAsync(fromUtc, toUtc);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Total program cost report: every reward disbursed within the range, split wallet-credit vs coupon (task 171).</summary>
    [HttpGet("reports/cost")]
    [Authorize(Policy = ReadPolicy)]
    [ProducesResponseType(typeof(ReferralCostReportResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CostReport([FromQuery] DateTime? fromUtc, [FromQuery] DateTime? toUtc)
    {
        var result = await _referralAdminService.GetCostReportAsync(fromUtc, toUtc);
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

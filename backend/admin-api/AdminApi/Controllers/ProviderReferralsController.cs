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
/// Admin provider-referral list/detail and fraud review queue, mirrors
/// <see cref="ReferralsController"/> (funnel/cost reports intentionally not
/// included in this v1 - see PROVIDER-REFERRAL.md).
/// </summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/admin/provider-referral")]
[Authorize(AuthenticationSchemes = DependencyInjection.AdminJwtBearerScheme)]
public class ProviderReferralsController : ControllerBase
{
    private const string ReadPolicy = AdminModules.ProviderReferral + ".read";
    private const string WritePolicy = AdminModules.ProviderReferral + ".write";

    private readonly IProviderReferralAdminService _referralAdminService;
    private readonly IProviderReferralFraudReviewService _fraudReviewService;
    private readonly IValidator<ProviderReferralAdminSearchRequest> _searchValidator;

    public ProviderReferralsController(
        IProviderReferralAdminService referralAdminService,
        IProviderReferralFraudReviewService fraudReviewService,
        IValidator<ProviderReferralAdminSearchRequest> searchValidator)
    {
        _referralAdminService = referralAdminService;
        _fraudReviewService = fraudReviewService;
        _searchValidator = searchValidator;
    }

    /// <summary>Filter by status, fraud flag, and/or search by provider.</summary>
    [HttpGet]
    [Authorize(Policy = ReadPolicy)]
    [ProducesResponseType(typeof(ProviderReferralAdminSearchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Search(
        [FromQuery] ProviderReferralStatus? status,
        [FromQuery] bool? isFraudFlagged,
        [FromQuery] string? providerSearch,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var request = new ProviderReferralAdminSearchRequest(status, isFraudFlagged, providerSearch, page, pageSize);

        var validation = await _searchValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        return Ok(await _referralAdminService.SearchAsync(request));
    }

    /// <summary>Only referrals currently flagged for fraud review - the fraud review queue.</summary>
    [HttpGet("fraud-queue")]
    [Authorize(Policy = ReadPolicy)]
    [ProducesResponseType(typeof(ProviderReferralAdminSearchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> FraudQueue([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var request = new ProviderReferralAdminSearchRequest(null, true, null, page, pageSize);

        var validation = await _searchValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        return Ok(await _referralAdminService.SearchAsync(request));
    }

    /// <summary>Provider referral detail view.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = ReadPolicy)]
    [ProducesResponseType(typeof(ProviderReferralAdminDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _referralAdminService.GetByIdAsync(id);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Manually flags a provider referral for fraud review.</summary>
    [HttpPost("{id:guid}/flag")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Flag(Guid id, [FromBody] ProviderReferralFraudReviewRequest? request)
    {
        var result = await _fraudReviewService.FlagAsync(id, CurrentAdminUserId(), request?.Note);
        return result.IsSuccess ? NoContent() : result.ToProblemResult();
    }

    /// <summary>Confirms a flagged referral as a real abuse pattern - the flag clears; any reward reversal is a separate, deliberate action.</summary>
    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ProviderReferralFraudReviewRequest? request)
    {
        var result = await _fraudReviewService.ApproveAsync(id, CurrentAdminUserId(), request?.Note);
        return result.IsSuccess ? NoContent() : result.ToProblemResult();
    }

    /// <summary>Rejects a flag as a false positive - the flag clears, no further action.</summary>
    [HttpPost("{id:guid}/reject")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] ProviderReferralFraudReviewRequest? request)
    {
        var result = await _fraudReviewService.RejectAsync(id, CurrentAdminUserId(), request?.Note);
        return result.IsSuccess ? NoContent() : result.ToProblemResult();
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

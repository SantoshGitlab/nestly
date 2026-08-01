using System.IdentityModel.Tokens.Jwt;
using Asp.Versioning;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Nestly.Application.PartnerManagement;
using Nestly.BuildingBlocks.Extensions;
using Nestly.Domain;
using Nestly.Infrastructure;

namespace Nestly.AdminApi.Controllers;

/// <summary>
/// Admin partner directory management (PARTNER.md API surface "Admin-Facing
/// Additions": Partner CRUD, KYC approval, performance; tasks 150a-150c,
/// 160). Read-only actions require "partner.read"; profile/status/KYC/
/// background-check mutations require "partner.write" - manual bank-transfer
/// earning adjustments are gated "payout.write" instead (see the earnings
/// endpoints below), matching the Partner/Payout RBAC split in PARTNER.md's
/// RBAC ADDITIONS section.
/// </summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/admin/partners")]
[Authorize(AuthenticationSchemes = DependencyInjection.AdminJwtBearerScheme)]
public class PartnersController : ControllerBase
{
    private const string ReadPolicy = AdminModules.Partner + ".read";
    private const string WritePolicy = AdminModules.Partner + ".write";
    private const string PayoutWritePolicy = AdminModules.Payout + ".write";

    private readonly IPartnerManagementService _partnerManagementService;
    private readonly IPartnerKycApprovalService _kycApprovalService;
    private readonly IPartnerEarningLedgerService _earningLedgerService;
    private readonly IValidator<PartnerSearchRequest> _searchValidator;
    private readonly IValidator<CreatePartnerRequest> _createValidator;
    private readonly IValidator<UpdatePartnerRequest> _updateValidator;
    private readonly IValidator<SuspendPartnerRequest> _suspendValidator;
    private readonly IValidator<RejectPartnerKycDocumentRequest> _rejectKycValidator;
    private readonly IValidator<RecordBackgroundCheckRequest> _backgroundCheckValidator;
    private readonly IValidator<RecordPartnerEarningAdjustmentRequest> _earningAdjustmentValidator;

    public PartnersController(
        IPartnerManagementService partnerManagementService,
        IPartnerKycApprovalService kycApprovalService,
        IPartnerEarningLedgerService earningLedgerService,
        IValidator<PartnerSearchRequest> searchValidator,
        IValidator<CreatePartnerRequest> createValidator,
        IValidator<UpdatePartnerRequest> updateValidator,
        IValidator<SuspendPartnerRequest> suspendValidator,
        IValidator<RejectPartnerKycDocumentRequest> rejectKycValidator,
        IValidator<RecordBackgroundCheckRequest> backgroundCheckValidator,
        IValidator<RecordPartnerEarningAdjustmentRequest> earningAdjustmentValidator)
    {
        _partnerManagementService = partnerManagementService;
        _kycApprovalService = kycApprovalService;
        _earningLedgerService = earningLedgerService;
        _searchValidator = searchValidator;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _suspendValidator = suspendValidator;
        _rejectKycValidator = rejectKycValidator;
        _backgroundCheckValidator = backgroundCheckValidator;
        _earningAdjustmentValidator = earningAdjustmentValidator;
    }

    // ---- CRUD (task 150a) ----

    /// <summary>Search/filter partners (task 150a).</summary>
    [HttpGet]
    [Authorize(Policy = ReadPolicy)]
    [ProducesResponseType(typeof(PartnerSearchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Search(
        [FromQuery] string? name,
        [FromQuery] string? phone,
        [FromQuery] PartnerStatus? status,
        [FromQuery] PartnerOnboardingStatus? onboardingStatus,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var request = new PartnerSearchRequest(name, phone, status, onboardingStatus, page, pageSize);
        var validation = await _searchValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _partnerManagementService.SearchAsync(request);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Partner detail: profile, KYC documents, background check history (task 150a/150b).</summary>
    [HttpGet("{partnerId:guid}")]
    [Authorize(Policy = ReadPolicy)]
    [ProducesResponseType(typeof(PartnerDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDetail(Guid partnerId)
    {
        var result = await _partnerManagementService.GetDetailAsync(partnerId);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Admin-created partner record (task 150a). PartnerType is always Individual - OPEN DECISIONS #2.</summary>
    [HttpPost]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(typeof(PartnerDetailResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreatePartnerRequest request)
    {
        var validation = await _createValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _partnerManagementService.CreateAsync(request);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Updates a partner's profile (task 150a).</summary>
    [HttpPut("{partnerId:guid}")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(typeof(PartnerDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid partnerId, [FromBody] UpdatePartnerRequest request)
    {
        var validation = await _updateValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _partnerManagementService.UpdateAsync(partnerId, request);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Suspends a partner's account (task 150a, PARTNER.md RBAC ADDITIONS "Suspend").</summary>
    [HttpPost("{partnerId:guid}/suspend")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(typeof(PartnerDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Suspend(Guid partnerId, [FromBody] SuspendPartnerRequest request)
    {
        var validation = await _suspendValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _partnerManagementService.SuspendAsync(partnerId, request);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Reactivates a previously suspended partner (task 150a).</summary>
    [HttpPost("{partnerId:guid}/reactivate")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(typeof(PartnerDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Reactivate(Guid partnerId)
    {
        var result = await _partnerManagementService.ReactivateAsync(partnerId);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    // ---- KYC approval and background check / activation (task 150b, 160) ----

    /// <summary>Approves a submitted KYC document (task 150b, the admin-side counterpart to task 146c's submission flow).</summary>
    [HttpPost("kyc-documents/{documentId:guid}/approve")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(typeof(PartnerKycDocumentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ApproveKycDocument(Guid documentId)
    {
        var result = await _kycApprovalService.ApproveDocumentAsync(documentId, CurrentAdminUserId());
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Rejects a submitted KYC document (task 150b).</summary>
    [HttpPost("kyc-documents/{documentId:guid}/reject")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(typeof(PartnerKycDocumentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RejectKycDocument(Guid documentId, [FromBody] RejectPartnerKycDocumentRequest request)
    {
        var validation = await _rejectKycValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _kycApprovalService.RejectDocumentAsync(documentId, CurrentAdminUserId(), request);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Records a background/reference check outcome (task 160) - a distinct step from KYC document validation.</summary>
    [HttpPost("{partnerId:guid}/background-check")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(typeof(PartnerBackgroundCheckResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RecordBackgroundCheck(Guid partnerId, [FromBody] RecordBackgroundCheckRequest request)
    {
        var validation = await _backgroundCheckValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _kycApprovalService.RecordBackgroundCheckAsync(partnerId, CurrentAdminUserId(), request);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Activates a partner once KYC is approved and the background check has passed (task 160's gate).</summary>
    [HttpPost("{partnerId:guid}/activate")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(typeof(PartnerDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Activate(Guid partnerId)
    {
        var result = await _kycApprovalService.ActivateAsync(partnerId);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    // ---- Performance (task 150c) ----

    /// <summary>Job-fulfilment performance summary (PARTNER.md API surface "get partner performance metrics", task 150c).</summary>
    [HttpGet("{partnerId:guid}/performance")]
    [Authorize(Policy = ReadPolicy)]
    [ProducesResponseType(typeof(PartnerPerformanceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPerformance(Guid partnerId)
    {
        var result = await _partnerManagementService.GetPerformanceAsync(partnerId);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    // ---- Earnings ledger (task 148) ----

    /// <summary>A partner's earning ledger and current balance (task 148).</summary>
    [HttpGet("{partnerId:guid}/earnings")]
    [Authorize(Policy = ReadPolicy)]
    [ProducesResponseType(typeof(PartnerEarningsSummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEarnings(Guid partnerId)
    {
        var result = await _earningLedgerService.GetSummaryAsync(partnerId);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>
    /// Records a manual credit/debit adjustment to a partner's earning
    /// ledger (task 148 - "credit per completed job ... debit for
    /// penalties"). Gated "payout.write" rather than "partner.write" - this
    /// is a financial-ledger mutation, the same RBAC tier as processing a
    /// payout, not a partner-profile edit.
    /// </summary>
    [HttpPost("{partnerId:guid}/earnings")]
    [Authorize(Policy = PayoutWritePolicy)]
    [ProducesResponseType(typeof(PartnerEarningLedgerEntryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RecordEarningAdjustment(Guid partnerId, [FromBody] RecordPartnerEarningAdjustmentRequest request)
    {
        var validation = await _earningAdjustmentValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _earningLedgerService.RecordAdjustmentAsync(partnerId, request);
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

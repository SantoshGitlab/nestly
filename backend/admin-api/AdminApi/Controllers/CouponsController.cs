using Asp.Versioning;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Nestly.Application.Coupons;
using Nestly.BuildingBlocks.Extensions;
using Nestly.Domain;
using Nestly.Infrastructure;

namespace Nestly.AdminApi.Controllers;

/// <summary>
/// Admin coupon and campaign management (SRS 12.12, task 118): coupon CRUD
/// with every rule dimension (discount type/value, min order value,
/// applicable category, usage limits, validity window, first/repeat-order
/// segment) plus redemption reporting. Read-only actions require
/// "coupons.read"; every mutating action requires "coupons.write" (task
/// 96b/96c) - a role granted Write always also holds Read (see
/// <c>AdminPermissionCatalog</c>), so the two are applied per-action rather
/// than a single class-level policy, matching <c>CustomersController</c>.
/// </summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/admin/coupons")]
[Authorize(AuthenticationSchemes = DependencyInjection.AdminJwtBearerScheme)]
public class CouponsController : ControllerBase
{
    private const string ReadPolicy = AdminModules.Coupons + ".read";
    private const string WritePolicy = AdminModules.Coupons + ".write";

    private readonly ICouponManagementService _couponManagementService;
    private readonly IValidator<CouponAdminSearchRequest> _searchValidator;
    private readonly IValidator<CouponCreateRequest> _createValidator;
    private readonly IValidator<CouponUpdateRequest> _updateValidator;

    public CouponsController(
        ICouponManagementService couponManagementService,
        IValidator<CouponAdminSearchRequest> searchValidator,
        IValidator<CouponCreateRequest> createValidator,
        IValidator<CouponUpdateRequest> updateValidator)
    {
        _couponManagementService = couponManagementService;
        _searchValidator = searchValidator;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    /// <summary>Active categories, for the coupon form's "applicable category" picker (see <see cref="ICouponManagementService.ListApplicableCategoriesAsync"/>).</summary>
    [HttpGet("categories")]
    [Authorize(Policy = ReadPolicy)]
    [ProducesResponseType(typeof(IReadOnlyList<Nestly.Application.Serviceability.CategoryLookupResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListApplicableCategories() => Ok(await _couponManagementService.ListApplicableCategoriesAsync());

    /// <summary>Search/filter coupons (SRS 12.12.1, task 118).</summary>
    [HttpGet]
    [Authorize(Policy = ReadPolicy)]
    [ProducesResponseType(typeof(CouponAdminSearchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Search(
        [FromQuery] string? code,
        [FromQuery] bool? isActive,
        [FromQuery] CouponDiscountType? discountType,
        [FromQuery] CouponCustomerSegment? customerSegment,
        [FromQuery] Guid? applicableCategoryId,
        [FromQuery] DateTime? validOnUtc,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var request = new CouponAdminSearchRequest(
            code, isActive, discountType, customerSegment, applicableCategoryId, validOnUtc, page, pageSize);

        var validation = await _searchValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        return Ok(await _couponManagementService.SearchAsync(request));
    }

    /// <summary>Coupon detail (SRS 12.12.1, task 118).</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = ReadPolicy)]
    [ProducesResponseType(typeof(CouponAdminResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _couponManagementService.GetByIdAsync(id);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Creates a coupon with every rule dimension (SRS 12.12.1, task 118).</summary>
    [HttpPost]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(typeof(CouponAdminResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CouponCreateRequest request)
    {
        var validation = await _createValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _couponManagementService.CreateAsync(request);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value)
            : result.ToProblemResult();
    }

    /// <summary>Edits every mutable rule dimension of an existing coupon (SRS 12.12.1, task 118). The coupon code itself is immutable - see <see cref="Coupon.Update"/>.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(typeof(CouponAdminResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] CouponUpdateRequest request)
    {
        var validation = await _updateValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _couponManagementService.UpdateAsync(id, request);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Re-enables a suspended coupon (SRS 12.12.1 "Active status").</summary>
    [HttpPost("{id:guid}/activate")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Activate(Guid id)
    {
        var result = await _couponManagementService.ActivateAsync(id);
        return result.IsSuccess ? NoContent() : result.ToProblemResult();
    }

    /// <summary>Suspends a coupon without deleting it (SRS 12.12.1 "Active status").</summary>
    [HttpPost("{id:guid}/deactivate")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        var result = await _couponManagementService.DeactivateAsync(id);
        return result.IsSuccess ? NoContent() : result.ToProblemResult();
    }

    /// <summary>Redemption reporting - aggregate stats from CouponRedemption (SRS 12.12.2, task 118).</summary>
    [HttpGet("redemptions/report")]
    [Authorize(Policy = ReadPolicy)]
    [ProducesResponseType(typeof(CouponRedemptionReportResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRedemptionReport(
        [FromQuery] Guid? couponId,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc)
    {
        var result = await _couponManagementService.GetRedemptionReportAsync(new CouponRedemptionReportRequest(couponId, fromUtc, toUtc));
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
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

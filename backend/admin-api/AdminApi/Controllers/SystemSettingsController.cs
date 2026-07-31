using Asp.Versioning;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Nestly.Application.Settings;
using Nestly.BuildingBlocks.Extensions;
using Nestly.Domain;
using Nestly.Infrastructure;

namespace Nestly.AdminApi.Controllers;

/// <summary>
/// Admin-configurable system settings/feature-flag management (SRS 12.19,
/// tasks 131a-131h): booking, slot, cancellation, reschedule, tax, wallet
/// and coupon settings groups, each independently readable/editable. Gated
/// behind "settings.read"/"settings.write" - the same two policies every
/// module's <see cref="AdminModules"/> code already generates via
/// <c>AdminPermissionCatalog</c>, so no new policy registration was needed.
/// </summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/settings")]
public class SystemSettingsController : ControllerBase
{
    private const string SettingsReadPolicy = AdminModules.Settings + ".read";
    private const string SettingsWritePolicy = AdminModules.Settings + ".write";

    private readonly ISystemSettingsService _settingsService;
    private readonly IValidator<BookingSettings> _bookingValidator;
    private readonly IValidator<SlotSettings> _slotValidator;
    private readonly IValidator<CancellationSettings> _cancellationValidator;
    private readonly IValidator<RescheduleSettings> _rescheduleValidator;
    private readonly IValidator<TaxSettings> _taxValidator;
    private readonly IValidator<WalletSettings> _walletValidator;
    private readonly IValidator<CouponSettings> _couponValidator;

    public SystemSettingsController(
        ISystemSettingsService settingsService,
        IValidator<BookingSettings> bookingValidator,
        IValidator<SlotSettings> slotValidator,
        IValidator<CancellationSettings> cancellationValidator,
        IValidator<RescheduleSettings> rescheduleValidator,
        IValidator<TaxSettings> taxValidator,
        IValidator<WalletSettings> walletValidator,
        IValidator<CouponSettings> couponValidator)
    {
        _settingsService = settingsService;
        _bookingValidator = bookingValidator;
        _slotValidator = slotValidator;
        _cancellationValidator = cancellationValidator;
        _rescheduleValidator = rescheduleValidator;
        _taxValidator = taxValidator;
        _walletValidator = walletValidator;
        _couponValidator = couponValidator;
    }

    /// <summary>Every settings group at once, for the admin Settings landing page.</summary>
    [HttpGet]
    [Authorize(AuthenticationSchemes = DependencyInjection.AdminJwtBearerScheme, Policy = SettingsReadPolicy)]
    [ProducesResponseType(typeof(AllSystemSettingsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAll()
    {
        var result = await _settingsService.GetAllAsync(HttpContext.RequestAborted);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    [HttpGet("booking")]
    [Authorize(AuthenticationSchemes = DependencyInjection.AdminJwtBearerScheme, Policy = SettingsReadPolicy)]
    [ProducesResponseType(typeof(BookingSettings), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBooking()
    {
        var result = await _settingsService.GetBookingSettingsAsync(HttpContext.RequestAborted);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    [HttpPut("booking")]
    [Authorize(AuthenticationSchemes = DependencyInjection.AdminJwtBearerScheme, Policy = SettingsWritePolicy)]
    [ProducesResponseType(typeof(BookingSettings), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateBooking([FromBody] BookingSettings request)
    {
        var validation = await _bookingValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _settingsService.UpdateBookingSettingsAsync(request, HttpContext.RequestAborted);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    [HttpGet("slot")]
    [Authorize(AuthenticationSchemes = DependencyInjection.AdminJwtBearerScheme, Policy = SettingsReadPolicy)]
    [ProducesResponseType(typeof(SlotSettings), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSlot()
    {
        var result = await _settingsService.GetSlotSettingsAsync(HttpContext.RequestAborted);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    [HttpPut("slot")]
    [Authorize(AuthenticationSchemes = DependencyInjection.AdminJwtBearerScheme, Policy = SettingsWritePolicy)]
    [ProducesResponseType(typeof(SlotSettings), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateSlot([FromBody] SlotSettings request)
    {
        var validation = await _slotValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _settingsService.UpdateSlotSettingsAsync(request, HttpContext.RequestAborted);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    [HttpGet("cancellation")]
    [Authorize(AuthenticationSchemes = DependencyInjection.AdminJwtBearerScheme, Policy = SettingsReadPolicy)]
    [ProducesResponseType(typeof(CancellationSettings), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCancellation()
    {
        var result = await _settingsService.GetCancellationSettingsAsync(HttpContext.RequestAborted);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    [HttpPut("cancellation")]
    [Authorize(AuthenticationSchemes = DependencyInjection.AdminJwtBearerScheme, Policy = SettingsWritePolicy)]
    [ProducesResponseType(typeof(CancellationSettings), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateCancellation([FromBody] CancellationSettings request)
    {
        var validation = await _cancellationValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _settingsService.UpdateCancellationSettingsAsync(request, HttpContext.RequestAborted);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    [HttpGet("reschedule")]
    [Authorize(AuthenticationSchemes = DependencyInjection.AdminJwtBearerScheme, Policy = SettingsReadPolicy)]
    [ProducesResponseType(typeof(RescheduleSettings), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetReschedule()
    {
        var result = await _settingsService.GetRescheduleSettingsAsync(HttpContext.RequestAborted);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    [HttpPut("reschedule")]
    [Authorize(AuthenticationSchemes = DependencyInjection.AdminJwtBearerScheme, Policy = SettingsWritePolicy)]
    [ProducesResponseType(typeof(RescheduleSettings), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateReschedule([FromBody] RescheduleSettings request)
    {
        var validation = await _rescheduleValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _settingsService.UpdateRescheduleSettingsAsync(request, HttpContext.RequestAborted);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    [HttpGet("tax")]
    [Authorize(AuthenticationSchemes = DependencyInjection.AdminJwtBearerScheme, Policy = SettingsReadPolicy)]
    [ProducesResponseType(typeof(TaxSettings), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTax()
    {
        var result = await _settingsService.GetTaxSettingsAsync(HttpContext.RequestAborted);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    [HttpPut("tax")]
    [Authorize(AuthenticationSchemes = DependencyInjection.AdminJwtBearerScheme, Policy = SettingsWritePolicy)]
    [ProducesResponseType(typeof(TaxSettings), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateTax([FromBody] TaxSettings request)
    {
        var validation = await _taxValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _settingsService.UpdateTaxSettingsAsync(request, HttpContext.RequestAborted);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    [HttpGet("wallet")]
    [Authorize(AuthenticationSchemes = DependencyInjection.AdminJwtBearerScheme, Policy = SettingsReadPolicy)]
    [ProducesResponseType(typeof(WalletSettings), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetWallet()
    {
        var result = await _settingsService.GetWalletSettingsAsync(HttpContext.RequestAborted);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    [HttpPut("wallet")]
    [Authorize(AuthenticationSchemes = DependencyInjection.AdminJwtBearerScheme, Policy = SettingsWritePolicy)]
    [ProducesResponseType(typeof(WalletSettings), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateWallet([FromBody] WalletSettings request)
    {
        var validation = await _walletValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _settingsService.UpdateWalletSettingsAsync(request, HttpContext.RequestAborted);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    [HttpGet("coupon")]
    [Authorize(AuthenticationSchemes = DependencyInjection.AdminJwtBearerScheme, Policy = SettingsReadPolicy)]
    [ProducesResponseType(typeof(CouponSettings), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCoupon()
    {
        var result = await _settingsService.GetCouponSettingsAsync(HttpContext.RequestAborted);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    [HttpPut("coupon")]
    [Authorize(AuthenticationSchemes = DependencyInjection.AdminJwtBearerScheme, Policy = SettingsWritePolicy)]
    [ProducesResponseType(typeof(CouponSettings), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateCoupon([FromBody] CouponSettings request)
    {
        var validation = await _couponValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _settingsService.UpdateCouponSettingsAsync(request, HttpContext.RequestAborted);
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

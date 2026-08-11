using Asp.Versioning;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Nestly.Application.RecurringBookings;
using Nestly.BuildingBlocks.Extensions;
using Nestly.Domain;
using Nestly.Infrastructure;

namespace Nestly.AdminApi.Controllers;

/// <summary>
/// Admin visibility into recurring booking plans (task 299,
/// PRODUCT-ENHANCEMENTS.md section 2): the full plan list and the
/// status/cadence/upcoming-volume report behind it. Read-only - see
/// <see cref="IRecurringBookingPlanAdminService"/> on why no admin
/// pause/resume/cancel is offered here.
///
/// RBAC: gated behind the EXISTING "bookings.read", with no new
/// <c>AdminModules</c> entry and no "RecurringPlans.View" code. The task brief
/// left that open ("no new RBAC module needed if admin's existing Booking view
/// permission already covers occurrence rows"); it does, for three reasons:
///
/// 1. A recurring plan is a standing instruction to create Bookings, and every
///    row this controller reports on is either a <see cref="RecurringBookingPlan"/>
///    or a <see cref="Booking"/> carrying that plan's id (task 296's
///    <see cref="Booking.RecurringBookingPlanId"/>). An admin holding
///    "bookings.read" can already open every one of those bookings
///    individually through <c>BookingsController</c> and read strictly more
///    about each of them (customer contact details, payment, refunds) than
///    this controller's counts expose. A new permission gating a strictly
///    weaker view of data the holder can already see is not a boundary, it is
///    an inconvenience - and one that fails open, because the underlying
///    bookings stay readable either way.
///
/// 2. <c>BookingsController</c> already set this precedent in the opposite
///    direction: provider assignment lives under "bookings.write" rather than
///    the Provider module's, because assigning a provider is Booking-domain
///    behaviour. Recurrence is likewise a property of how bookings come into
///    existence, not a separate vertical.
///
/// 3. <c>AdminPermissionAction</c>'s own doc comment calls splitting the
///    matrix further "speculative (YAGNI)" until a controller actually needs
///    the distinction, and <c>AdminModules</c> records the same judgement for
///    Referral/Chat/Nestly Coins. A new module here would also cost a seed
///    migration (<c>SeedNestlyCoinsPermissions</c> is the precedent) and a
///    role-grant decision for all nine default roles - real schema and policy
///    churn bought for no additional protection.
///
/// The practical consequence is intended: Operations Admin and Booking Admin,
/// the two roles that own day-to-day fulfilment, see recurring plans on day
/// one without a permission grant, exactly as they see the bookings those
/// plans generate.
/// </summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/admin/recurring-plans")]
[Authorize(AuthenticationSchemes = DependencyInjection.AdminJwtBearerScheme)]
public class RecurringPlansController : ControllerBase
{
    private const string ReadPolicy = AdminModules.Bookings + ".read";

    private readonly IRecurringBookingPlanAdminService _adminService;
    private readonly IValidator<AdminRecurringPlanSearchRequest> _searchValidator;
    private readonly IValidator<AdminRecurringPlanReportRequest> _reportValidator;

    public RecurringPlansController(
        IRecurringBookingPlanAdminService adminService,
        IValidator<AdminRecurringPlanSearchRequest> searchValidator,
        IValidator<AdminRecurringPlanReportRequest> reportValidator)
    {
        _adminService = adminService;
        _searchValidator = searchValidator;
        _reportValidator = reportValidator;
    }

    /// <summary>Every recurring plan on the platform, newest first, filterable by lifecycle status, cadence, customer or service.</summary>
    [HttpGet]
    [Authorize(Policy = ReadPolicy)]
    [ProducesResponseType(typeof(AdminRecurringPlanSearchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Search(
        [FromQuery] RecurringBookingPlanStatus? status,
        [FromQuery] RecurringBookingRecurrenceFrequency? frequency,
        [FromQuery] Guid? customerId,
        [FromQuery] Guid? serviceId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var request = new AdminRecurringPlanSearchRequest(status, frequency, customerId, serviceId, page, pageSize);

        var validation = await _searchValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _adminService.SearchAsync(request);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Active/paused/cancelled/completed plan counts, the active-plan cadence mix, and upcoming occurrence volume over a horizon (defaults to the next four weeks).</summary>
    [HttpGet("report")]
    [Authorize(Policy = ReadPolicy)]
    [ProducesResponseType(typeof(AdminRecurringPlanReportResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetReport([FromQuery] DateOnly? fromDate, [FromQuery] DateOnly? toDate)
    {
        var request = new AdminRecurringPlanReportRequest(fromDate, toDate);

        var validation = await _reportValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _adminService.GetReportAsync(request);
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

using Asp.Versioning;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Nestly.Application.Support;
using Nestly.BuildingBlocks.Extensions;
using Nestly.Domain;
using Nestly.Infrastructure;

namespace Nestly.AdminApi.Controllers;

/// <summary>
/// Admin ticket workflow (SRS 12.14, 16.2, tasks 120a-f): search/detail
/// across every customer, assign/unassign, respond, escalate, resolve/close,
/// and link a booking. Read-only actions require "support.read"; every
/// mutating action requires "support.write" (task 96b/96c) - same per-action
/// split as <see cref="CouponsController"/>. The formal dispute mark/resolve
/// sub-flow (task 155) stays on its own <see cref="SupportTicketDisputesController"/>
/// - this controller does not duplicate it.
///
/// <para>
/// Booking link (task 120e): <see cref="LinkBooking"/> only records/validates
/// a booking reference and returns its read-only summary
/// (<see cref="LinkedBookingSummaryResponse"/>) - there is no admin
/// booking-management API in this codebase yet to call cancel/refund against
/// (BookingMgmt is a separate, not-yet-landed vertical). TODO(BookingMgmt):
/// once an admin booking controller exists, add cancel/refund shortcut
/// endpoints here (or have the admin-web UI call that controller directly
/// using the linked booking's id) instead of read-only summary only.
/// </para>
/// </summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/admin/support-tickets")]
[Authorize(AuthenticationSchemes = DependencyInjection.AdminJwtBearerScheme)]
public class SupportTicketsController : ControllerBase
{
    private const string ReadPolicy = AdminModules.Support + ".read";
    private const string WritePolicy = AdminModules.Support + ".write";

    private readonly IAdminSupportTicketService _service;
    private readonly IValidator<AdminSupportTicketSearchRequest> _searchValidator;
    private readonly IValidator<AssignSupportTicketRequest> _assignValidator;
    private readonly IValidator<AddSupportTicketCommentRequest> _respondValidator;
    private readonly IValidator<ResolveSupportTicketRequest> _resolveValidator;
    private readonly IValidator<LinkSupportTicketBookingRequest> _linkBookingValidator;

    public SupportTicketsController(
        IAdminSupportTicketService service,
        IValidator<AdminSupportTicketSearchRequest> searchValidator,
        IValidator<AssignSupportTicketRequest> assignValidator,
        IValidator<AddSupportTicketCommentRequest> respondValidator,
        IValidator<ResolveSupportTicketRequest> resolveValidator,
        IValidator<LinkSupportTicketBookingRequest> linkBookingValidator)
    {
        _service = service;
        _searchValidator = searchValidator;
        _assignValidator = assignValidator;
        _respondValidator = respondValidator;
        _resolveValidator = resolveValidator;
        _linkBookingValidator = linkBookingValidator;
    }

    /// <summary>Filtered/paginated ticket search across every customer (SRS 12.14.1: Ticket ID via GetById, Booking ID, Customer, Category, Priority, Status, Assigned agent, Date range).</summary>
    [HttpGet]
    [Authorize(Policy = ReadPolicy)]
    [ProducesResponseType(typeof(AdminSupportTicketSearchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Search(
        [FromQuery] Guid? customerId,
        [FromQuery] Guid? bookingId,
        [FromQuery] SupportTicketCategory? category,
        [FromQuery] SupportTicketPriority? priority,
        [FromQuery] SupportTicketStatus? status,
        [FromQuery] Guid? assignedAdminUserId,
        [FromQuery] bool? unassigned,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var request = new AdminSupportTicketSearchRequest(
            customerId, bookingId, category, priority, status, assignedAdminUserId, unassigned, fromUtc, toUtc, page, pageSize);

        var validation = await _searchValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _service.SearchAsync(request);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Every active admin, for the "assign to" picker (task 120a). Registered before the "{id:guid}" route below so it is never captured as an id.</summary>
    [HttpGet("assignable-admins")]
    [Authorize(Policy = ReadPolicy)]
    [ProducesResponseType(typeof(IReadOnlyList<AssignableAdminResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAssignableAdmins()
    {
        var result = await _service.ListAssignableAdminsAsync();
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Full ticket detail - comment thread, assignee, and linked booking summary if any (SRS 16.3, task 120f).</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = ReadPolicy)]
    [ProducesResponseType(typeof(AdminSupportTicketDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _service.GetDetailAsync(id);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Assigns a ticket to an admin/agent (SRS 12.14.2 "Assign to team/user", task 120a).</summary>
    [HttpPost("{id:guid}/assign")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(typeof(AdminSupportTicketDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Assign(Guid id, [FromBody] AssignSupportTicketRequest request)
    {
        var validation = await _assignValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _service.AssignAsync(id, request);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Clears a ticket's current assignment (task 120a).</summary>
    [HttpPost("{id:guid}/unassign")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(typeof(AdminSupportTicketDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Unassign(Guid id)
    {
        var result = await _service.UnassignAsync(id);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Appends an admin response to the ticket's comment thread (SRS 12.14.2 "Add response/note", task 120b).</summary>
    [HttpPost("{id:guid}/respond")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(typeof(AdminSupportTicketDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Respond(Guid id, [FromBody] AddSupportTicketCommentRequest request)
    {
        var validation = await _respondValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _service.RespondAsync(id, request);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Moves the ticket to Escalated (SRS 12.14.2 "Mark escalated", task 120c).</summary>
    [HttpPost("{id:guid}/escalate")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(typeof(AdminSupportTicketDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Escalate(Guid id)
    {
        var result = await _service.EscalateAsync(id);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Moves the ticket to Resolved (SRS 12.14.2 "Mark resolved/closed", task 120d) - for tickets with no formal dispute open; use <see cref="SupportTicketDisputesController"/> instead when one is.</summary>
    [HttpPost("{id:guid}/resolve")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(typeof(AdminSupportTicketDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Resolve(Guid id, [FromBody] ResolveSupportTicketRequest request)
    {
        var validation = await _resolveValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _service.ResolveAsync(id, request);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Moves the ticket to Closed (SRS 12.14.2 "Mark resolved/closed", task 120d).</summary>
    [HttpPost("{id:guid}/close")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(typeof(AdminSupportTicketDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Close(Guid id)
    {
        var result = await _service.CloseAsync(id);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Attaches (or re-attaches) a booking to the ticket (SRS 12.14.2 "Link ... booking action", task 120e). See this controller's own doc comment for the cancel/refund-shortcut TODO.</summary>
    [HttpPost("{id:guid}/link-booking")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(typeof(AdminSupportTicketDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> LinkBooking(Guid id, [FromBody] LinkSupportTicketBookingRequest request)
    {
        var validation = await _linkBookingValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _service.LinkBookingAsync(id, request);
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

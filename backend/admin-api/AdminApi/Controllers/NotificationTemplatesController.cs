using Asp.Versioning;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Nestly.Application.Notifications;
using Nestly.BuildingBlocks.Extensions;
using Nestly.Domain;
using Nestly.Infrastructure;

namespace Nestly.AdminApi.Controllers;

/// <summary>
/// Admin notification template management (SRS 12.17, tasks 126a-d): CRUD
/// over channel-specific templates with variable placeholders, preview/test
/// rendering, and change history via the existing audit trail. Read-only
/// actions require "notifications.read"; every mutating action requires
/// "notifications.write" (task 96b/96c) - applied per-action rather than a
/// single class-level policy, matching <c>CouponsController</c>.
/// </summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/admin/notification-templates")]
[Authorize(AuthenticationSchemes = DependencyInjection.AdminJwtBearerScheme)]
public class NotificationTemplatesController : ControllerBase
{
    private const string ReadPolicy = AdminModules.Notifications + ".read";
    private const string WritePolicy = AdminModules.Notifications + ".write";

    private readonly INotificationTemplateManagementService _templateManagementService;
    private readonly IValidator<NotificationTemplateCreateRequest> _createValidator;
    private readonly IValidator<NotificationTemplateUpdateRequest> _updateValidator;
    private readonly IValidator<NotificationTemplateAdHocPreviewRequest> _adHocPreviewValidator;

    public NotificationTemplatesController(
        INotificationTemplateManagementService templateManagementService,
        IValidator<NotificationTemplateCreateRequest> createValidator,
        IValidator<NotificationTemplateUpdateRequest> updateValidator,
        IValidator<NotificationTemplateAdHocPreviewRequest> adHocPreviewValidator)
    {
        _templateManagementService = templateManagementService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _adHocPreviewValidator = adHocPreviewValidator;
    }

    /// <summary>Lists templates, optionally filtered by channel/event type/active status (SRS 12.17.1-2).</summary>
    [HttpGet]
    [Authorize(Policy = ReadPolicy)]
    [ProducesResponseType(typeof(IReadOnlyList<NotificationTemplateResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] NotificationChannel? channel,
        [FromQuery] NotificationEventType? eventType,
        [FromQuery] bool? isActive,
        CancellationToken cancellationToken) =>
        Ok(await _templateManagementService.ListAsync(channel, eventType, isActive, cancellationToken));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = ReadPolicy)]
    [ProducesResponseType(typeof(NotificationTemplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _templateManagementService.GetByIdAsync(id, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Creates a template for a not-yet-covered (EventType, Channel) combination (SRS 12.17.1-2).</summary>
    [HttpPost]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(typeof(NotificationTemplateResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] NotificationTemplateCreateRequest request, CancellationToken cancellationToken)
    {
        var validation = await _createValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _templateManagementService.CreateAsync(request, cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value)
            : result.ToProblemResult();
    }

    /// <summary>Edits an existing template's subject/body (SRS 12.17.2). Event type, channel and template key are immutable - see <see cref="NotificationTemplate"/>'s doc comment.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(typeof(NotificationTemplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] NotificationTemplateUpdateRequest request, CancellationToken cancellationToken)
    {
        var validation = await _updateValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _templateManagementService.UpdateAsync(id, request, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    [HttpPost("{id:guid}/activate")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken cancellationToken)
    {
        var result = await _templateManagementService.ActivateAsync(id, cancellationToken);
        return result.IsSuccess ? NoContent() : result.ToProblemResult();
    }

    [HttpPost("{id:guid}/deactivate")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        var result = await _templateManagementService.DeactivateAsync(id, cancellationToken);
        return result.IsSuccess ? NoContent() : result.ToProblemResult();
    }

    /// <summary>Renders a saved template against sample values (SRS 12.17.2 "Preview/test capability", task 126b) - a pure render, nothing is sent or persisted.</summary>
    [HttpPost("{id:guid}/preview")]
    [Authorize(Policy = ReadPolicy)]
    [ProducesResponseType(typeof(NotificationTemplatePreviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Preview(Guid id, [FromBody] NotificationTemplatePreviewRequest request, CancellationToken cancellationToken)
    {
        var result = await _templateManagementService.PreviewAsync(id, request, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Renders draft (not-yet-saved) subject/body text against sample values, for the template editor's live preview (task 127).</summary>
    [HttpPost("preview")]
    [Authorize(Policy = ReadPolicy)]
    [ProducesResponseType(typeof(NotificationTemplatePreviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PreviewAdHoc([FromBody] NotificationTemplateAdHocPreviewRequest request, CancellationToken cancellationToken)
    {
        var validation = await _adHocPreviewValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        return Ok(_templateManagementService.PreviewAdHoc(request));
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

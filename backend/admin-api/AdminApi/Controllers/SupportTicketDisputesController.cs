using Asp.Versioning;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Nestly.Application.Support;
using Nestly.BuildingBlocks.Extensions;

namespace Nestly.AdminApi.Controllers;

/// <summary>
/// Admin dispute mark/resolve workflow on a support ticket (SRS 11.18.1
/// "wrong charge / pricing dispute", task 155).
///
/// Deliberately not <c>[Authorize]</c>-gated: admin authentication/
/// authorization does not exist anywhere in this codebase yet (that's
/// Phase 6 - see AGENTS.md's audit notes and AdminApi's own <c>Program.cs</c>,
/// which has never called <c>AddJwtAuthentication</c> or <c>UseAuthentication</c>).
/// Rather than block this task on a prerequisite that isn't built, the real
/// capability lives in <see cref="IDisputeResolutionService"/> and this is a
/// plain internal endpoint exposing it - Phase 6 wires the admin-role check
/// in front of this action, not a rewrite of the workflow itself.
/// </summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/support-tickets/{ticketId:guid}/dispute")]
public class SupportTicketDisputesController : ControllerBase
{
    private readonly IDisputeResolutionService _disputeResolutionService;
    private readonly IValidator<ResolveDisputeRequest> _resolveValidator;

    public SupportTicketDisputesController(IDisputeResolutionService disputeResolutionService, IValidator<ResolveDisputeRequest> resolveValidator)
    {
        _disputeResolutionService = disputeResolutionService;
        _resolveValidator = resolveValidator;
    }

    /// <summary>Admin opens a formal dispute investigation on a ticket.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(SupportTicketDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> MarkDisputed(Guid ticketId)
    {
        var result = await _disputeResolutionService.MarkDisputedAsync(ticketId);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Admin resolves an open dispute as refund (valid) or close/rework (invalid).</summary>
    [HttpPost("resolve")]
    [ProducesResponseType(typeof(DisputeResolutionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Resolve(Guid ticketId, [FromBody] ResolveDisputeRequest request)
    {
        var validation = await _resolveValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _disputeResolutionService.ResolveAsync(ticketId, request);
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

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
/// Admin dispute mark/resolve workflow on a support ticket (SRS 11.18.1
/// "wrong charge / pricing dispute", task 155).
///
/// Gated behind "support.write" (task 96b/96c) - opening or resolving a
/// dispute is a mutating support action, same module as the rest of the
/// ticket workflow. The real capability lives in
/// <see cref="IDisputeResolutionService"/>; this controller only adds the
/// permission gate in front of it.
/// </summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/support-tickets/{ticketId:guid}/dispute")]
[Authorize(AuthenticationSchemes = DependencyInjection.AdminJwtBearerScheme, Policy = SupportWritePolicy)]
public class SupportTicketDisputesController : ControllerBase
{
    private const string SupportWritePolicy = AdminModules.Support + ".write";

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
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
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
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
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

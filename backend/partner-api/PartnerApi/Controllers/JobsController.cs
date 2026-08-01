using System.IdentityModel.Tokens.Jwt;
using Asp.Versioning;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Nestly.Application.PartnerJobs;
using Nestly.BuildingBlocks.Extensions;
using Nestly.Infrastructure;

namespace Nestly.PartnerApi.Controllers;

/// <summary>
/// Partner jobs (task 149a, PARTNER.md API surface "Jobs" - list/detail,
/// accept/reject/start/complete, completion proof upload), wired to a real
/// <see cref="IPartnerJobService"/> backed by the <c>BookingPartnerAssignment</c>
/// bridge entity (task 147). Every action is scoped to the caller's own
/// partner id taken from the JWT - there is no route or body parameter that
/// could name a different partner (SRS 28.3 IDOR), same pattern as
/// <see cref="ProfileController"/>/<see cref="AvailabilityController"/>.
/// </summary>
[ApiController]
[ApiVersion(1)]
[Authorize(AuthenticationSchemes = DependencyInjection.PartnerJwtBearerScheme)]
[Route("api/v{version:apiVersion}/jobs")]
public class JobsController : ControllerBase
{
    private readonly IPartnerJobService _jobService;
    private readonly IValidator<RejectJobRequest> _rejectValidator;
    private readonly IValidator<UploadJobCompletionProofRequest> _completionProofValidator;

    public JobsController(
        IPartnerJobService jobService,
        IValidator<RejectJobRequest> rejectValidator,
        IValidator<UploadJobCompletionProofRequest> completionProofValidator)
    {
        _jobService = jobService;
        _rejectValidator = rejectValidator;
        _completionProofValidator = completionProofValidator;
    }

    /// <summary>List jobs ever assigned to the caller, optionally filtered by status and/or slot date.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PartnerJobSearchResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] PartnerJobStatus? status, [FromQuery] DateOnly? date)
    {
        var result = await _jobService.ListAsync(CurrentPartnerId(), status, date);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Get one job's detail.</summary>
    [HttpGet("{bookingId:guid}")]
    [ProducesResponseType(typeof(PartnerJobDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDetail(Guid bookingId)
    {
        var result = await _jobService.GetDetailAsync(CurrentPartnerId(), bookingId);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Accept an assigned job.</summary>
    [HttpPost("{bookingId:guid}/accept")]
    [ProducesResponseType(typeof(PartnerJobDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Accept(Guid bookingId)
    {
        var result = await _jobService.AcceptAsync(CurrentPartnerId(), bookingId);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Reject an assigned job (task 159 - returns the booking to the assignable pool for admin reassignment).</summary>
    [HttpPost("{bookingId:guid}/reject")]
    [ProducesResponseType(typeof(PartnerJobDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Reject(Guid bookingId, [FromBody] RejectJobRequest request)
    {
        var validation = await _rejectValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _jobService.RejectAsync(CurrentPartnerId(), bookingId, request);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Mark an accepted job as started (partner has arrived / begun work).</summary>
    [HttpPost("{bookingId:guid}/start")]
    [ProducesResponseType(typeof(PartnerJobDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Start(Guid bookingId)
    {
        var result = await _jobService.StartAsync(CurrentPartnerId(), bookingId);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Mark an in-progress job as completed.</summary>
    [HttpPost("{bookingId:guid}/complete")]
    [ProducesResponseType(typeof(PartnerJobDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Complete(Guid bookingId)
    {
        var result = await _jobService.CompleteAsync(CurrentPartnerId(), bookingId);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Attach completion proof (photo/file reference) to a job.</summary>
    [HttpPost("{bookingId:guid}/completion-proof")]
    [ProducesResponseType(typeof(PartnerJobDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UploadCompletionProof(Guid bookingId, [FromBody] UploadJobCompletionProofRequest request)
    {
        var validation = await _completionProofValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _jobService.UploadCompletionProofAsync(CurrentPartnerId(), bookingId, request);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    private Guid CurrentPartnerId() =>
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

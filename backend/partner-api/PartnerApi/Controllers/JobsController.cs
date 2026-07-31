using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nestly.Infrastructure;

namespace Nestly.PartnerApi.Controllers;

/// <summary>
/// Partner jobs (task 149a, PARTNER.md API surface "Jobs" — list/detail,
/// accept/reject/start/complete, completion proof upload).
///
/// STUB: every action here returns 501 Not Implemented. PARTNER.md's
/// assignment bridge entity, <c>BookingPartnerAssignment</c>, is being built
/// concurrently on a sibling branch (task 147) and is not yet present in this
/// worktree. Route shapes and method signatures below match the API surface
/// PARTNER.md describes so wiring this up is a drop-in once 147 merges: swap
/// each body for a call into an <c>IPartnerJobService</c> (to be introduced
/// then) backed by <c>IBookingPartnerAssignmentRepository</c>, following the
/// same "resolve the caller's own partner id from the JWT, never trust a
/// route/body value" pattern as <see cref="ProfileController"/> and
/// <see cref="AvailabilityController"/>.
///
/// TODO(task 147): replace every action body once BookingPartnerAssignment lands.
/// </summary>
[ApiController]
[ApiVersion(1)]
[Authorize(AuthenticationSchemes = DependencyInjection.PartnerJwtBearerScheme)]
[Route("api/v{version:apiVersion}/jobs")]
public class JobsController : ControllerBase
{
    /// <summary>List jobs assigned to the caller, optionally filtered by status and/or date.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult List([FromQuery] string? status, [FromQuery] DateOnly? date) => NotImplementedPendingAssignmentBridge();

    /// <summary>Get one job's detail.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult GetDetail(Guid id) => NotImplementedPendingAssignmentBridge();

    /// <summary>Accept an assigned job.</summary>
    [HttpPost("{id:guid}/accept")]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult Accept(Guid id) => NotImplementedPendingAssignmentBridge();

    /// <summary>Reject an assigned job.</summary>
    [HttpPost("{id:guid}/reject")]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult Reject(Guid id) => NotImplementedPendingAssignmentBridge();

    /// <summary>Mark an accepted job as started (partner has arrived / begun work).</summary>
    [HttpPost("{id:guid}/start")]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult Start(Guid id) => NotImplementedPendingAssignmentBridge();

    /// <summary>Mark an in-progress job as completed.</summary>
    [HttpPost("{id:guid}/complete")]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult Complete(Guid id) => NotImplementedPendingAssignmentBridge();

    /// <summary>Attach completion proof (photo/file reference) to a job.</summary>
    [HttpPost("{id:guid}/completion-proof")]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult UploadCompletionProof(Guid id) => NotImplementedPendingAssignmentBridge();

    private ObjectResult NotImplementedPendingAssignmentBridge() =>
        StatusCode(StatusCodes.Status501NotImplemented, new ProblemDetails
        {
            Status = StatusCodes.Status501NotImplemented,
            Title = "Jobs.NotYetAvailable",
            Detail = "Job assignment is not available yet - it depends on the BookingPartnerAssignment " +
                     "bridge entity (task 147), which has not merged into this API yet."
        });
}

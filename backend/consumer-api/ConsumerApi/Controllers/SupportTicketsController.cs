using System.IdentityModel.Tokens.Jwt;
using Asp.Versioning;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Nestly.Application.Support;
using Nestly.BuildingBlocks.Extensions;

namespace Nestly.ConsumerApi.Controllers;

/// <summary>
/// Customer support tickets (SRS 11.18, 16, 24.8, tasks 86a-d) - both
/// booking-linked and generic (task 86d): <see cref="CreateSupportTicketRequest.BookingId"/>
/// carries the link when one applies. See <see cref="BookingSupportTicketsController"/>
/// for the booking-scoped listing view of the same tickets.
/// </summary>
[ApiController]
[ApiVersion(1)]
[Authorize]
[Route("api/v{version:apiVersion}/support-tickets")]
public class SupportTicketsController : ControllerBase
{
    private readonly ISupportTicketService _supportTicketService;
    private readonly IValidator<CreateSupportTicketRequest> _createValidator;
    private readonly IValidator<AddSupportTicketCommentRequest> _commentValidator;

    public SupportTicketsController(
        ISupportTicketService supportTicketService,
        IValidator<CreateSupportTicketRequest> createValidator,
        IValidator<AddSupportTicketCommentRequest> commentValidator)
    {
        _supportTicketService = supportTicketService;
        _createValidator = createValidator;
        _commentValidator = commentValidator;
    }

    /// <summary>Raises a new ticket (SRS 11.18.1-2, 24.8, task 86a).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(SupportTicketDetailResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create([FromBody] CreateSupportTicketRequest request)
    {
        var validation = await _createValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _supportTicketService.CreateAsync(CurrentCustomerId(), request);
        return result.IsSuccess ? CreatedAtAction(nameof(Detail), new { id = result.Value.Id }, result.Value) : result.ToProblemResult();
    }

    /// <summary>Lists all of the caller's tickets, newest first (SRS 24.8, task 86b).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<SupportTicketSummaryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List()
    {
        var result = await _supportTicketService.ListAsync(CurrentCustomerId());
        return Ok(result.Value);
    }

    /// <summary>Ticket detail with its full comment thread (SRS 11.18.3, 24.8, task 86c).</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(SupportTicketDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Detail(Guid id)
    {
        var result = await _supportTicketService.GetDetailAsync(CurrentCustomerId(), id);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Appends a customer follow-up to the ticket's thread (SRS 11.18.3, 12.14.2).</summary>
    [HttpPost("{id:guid}/comments")]
    [ProducesResponseType(typeof(SupportTicketDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddComment(Guid id, [FromBody] AddSupportTicketCommentRequest request)
    {
        var validation = await _commentValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _supportTicketService.AddCommentAsync(CurrentCustomerId(), id, request);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    private Guid CurrentCustomerId() =>
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

using System.IdentityModel.Tokens.Jwt;
using Asp.Versioning;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Nestly.Application.Bookings;
using Nestly.BuildingBlocks.Extensions;
using Nestly.Domain;

namespace Nestly.ConsumerApi.Controllers;

/// <summary>Booking (SRS 13). Every action is scoped to the caller's own customer id — never a route/body parameter.</summary>
[ApiController]
[ApiVersion(1)]
[Authorize]
[Route("api/v{version:apiVersion}/bookings")]
public class BookingsController : ControllerBase
{
    private readonly IBookingSummaryService _bookingSummaryService;
    private readonly IBookingService _bookingService;
    private readonly IValidator<BookingSummaryRequest> _summaryValidator;

    public BookingsController(
        IBookingSummaryService bookingSummaryService,
        IBookingService bookingService,
        IValidator<BookingSummaryRequest> summaryValidator)
    {
        _bookingSummaryService = bookingSummaryService;
        _bookingService = bookingService;
        _summaryValidator = summaryValidator;
    }

    /// <summary>Previews what booking would produce - price, slot, and policy summary - without persisting anything (SRS 11.7, task 57).</summary>
    [HttpPost("summary")]
    [ProducesResponseType(typeof(BookingSummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Summary([FromBody] BookingSummaryRequest request)
    {
        var validation = await _summaryValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _bookingSummaryService.GetSummaryAsync(CurrentCustomerId(), request);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Creates a booking (SRS 13, tasks 58-59). Re-validates every precondition the summary already checked - a summary is not a reservation.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(BookingDetailResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create([FromBody] BookingSummaryRequest request)
    {
        var validation = await _summaryValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _bookingService.CreateAsync(CurrentCustomerId(), request);
        return result.IsSuccess
            ? CreatedAtAction(nameof(Detail), new { id = result.Value.Id }, result.Value)
            : result.ToProblemResult();
    }

    /// <summary>Lists the caller's bookings, optionally filtered to a status bucket (SRS 11.13, task 60b).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<BookingListItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List([FromQuery] BookingStatusBucket? bucket)
    {
        var result = await _bookingService.ListAsync(CurrentCustomerId(), bucket);
        return Ok(result.Value);
    }

    /// <summary>Booking detail with its full status timeline (SRS 11.13, 24.6, task 60c).</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(BookingDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Detail(Guid id)
    {
        var result = await _bookingService.GetDetailAsync(CurrentCustomerId(), id);
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

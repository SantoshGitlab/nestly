using Asp.Versioning;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Nestly.Application.Bookings;
using Nestly.Application.Tracking;
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
    private readonly IBookingTrackingQueryService _trackingQueryService;
    private readonly IValidator<BookingSummaryRequest> _summaryValidator;

    public BookingsController(
        IBookingSummaryService bookingSummaryService,
        IBookingService bookingService,
        IBookingTrackingQueryService trackingQueryService,
        IValidator<BookingSummaryRequest> summaryValidator)
    {
        _bookingSummaryService = bookingSummaryService;
        _bookingService = bookingService;
        _trackingQueryService = trackingQueryService;
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

    /// <summary>Lists the caller's bookings, optionally filtered to a status bucket, newest first (SRS 11.13, task 60b). Paged - <paramref name="page"/>/<paramref name="pageSize"/> default to 1/20, same as the admin booking search.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(BookingListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List([FromQuery] BookingStatusBucket? bucket, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _bookingService.ListAsync(CurrentCustomerId(), bucket, page, pageSize);
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

    /// <summary>
    /// The live tracking snapshot for a booking in progress (task 275) - the
    /// one-shot read the tracking screen loads before the SignalR hub starts
    /// pushing updates into it.
    ///
    /// Narrower than <see cref="Detail"/> on purpose: status, who is coming
    /// (with a masked phone, never the raw number), where they are, when they
    /// are expected, and where they are heading. No 403 is documented because
    /// none is possible - someone else's booking is a 404, so this endpoint
    /// cannot be used to confirm a booking id exists.
    /// </summary>
    [HttpGet("{id:guid}/tracking")]
    [ProducesResponseType(typeof(BookingTrackingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Tracking(Guid id)
    {
        var result = await _trackingQueryService.GetForCustomerAsync(CurrentCustomerId(), id);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    private Guid CurrentCustomerId() =>
        User.GetSubjectId();

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

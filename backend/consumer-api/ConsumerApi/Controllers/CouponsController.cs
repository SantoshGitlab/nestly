using Asp.Versioning;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Nestly.Application.Bookings;
using Nestly.BuildingBlocks.Extensions;

namespace Nestly.ConsumerApi.Controllers;

/// <summary>
/// Coupon apply/remove at checkout (SRS 11.10.3, task 73). Both endpoints
/// delegate to <see cref="IBookingSummaryService"/> - the same server-side
/// validation and price recomputation the booking summary/preview already
/// performs - rather than duplicating coupon business logic here. "Apply"
/// and "remove" are the same recompute operation with a code present or
/// absent respectively; there is no server-side checkout session to mutate
/// (see the doc comment on <see cref="BookingSummaryRequest.CouponCode"/>),
/// so each call is a fresh, complete recomputation.
/// </summary>
[ApiController]
[ApiVersion(1)]
[Authorize]
[Route("api/v{version:apiVersion}/coupons")]
public class CouponsController : ControllerBase
{
    private readonly IBookingSummaryService _bookingSummaryService;
    private readonly IValidator<BookingSummaryRequest> _validator;

    public CouponsController(IBookingSummaryService bookingSummaryService, IValidator<BookingSummaryRequest> validator)
    {
        _bookingSummaryService = bookingSummaryService;
        _validator = validator;
    }

    /// <summary>Validates and applies a coupon code to the given cart context, returning the recomputed summary with the discount shown (SRS 11.10.3 "success message" / "meaningful error message").</summary>
    [HttpPost("apply")]
    [ProducesResponseType(typeof(BookingSummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Apply([FromBody] BookingSummaryRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CouponCode))
        {
            return ValidationProblem(SingleFieldError(nameof(request.CouponCode), "A coupon code is required."));
        }

        return await Recompute(request);
    }

    /// <summary>Removes any applied coupon and returns the recomputed summary at full price (SRS 11.10.3 "coupon removal shall recompute final payable amount").</summary>
    [HttpPost("remove")]
    [ProducesResponseType(typeof(BookingSummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Remove([FromBody] BookingSummaryRequest request) =>
        await Recompute(request with { CouponCode = null });

    private async Task<IActionResult> Recompute(BookingSummaryRequest request)
    {
        var validation = await _validator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _bookingSummaryService.GetSummaryAsync(CurrentCustomerId(), request);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    private Guid CurrentCustomerId() =>
        User.GetSubjectId();

    private static ModelStateDictionary SingleFieldError(string field, string message)
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError(field, message);
        return modelState;
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

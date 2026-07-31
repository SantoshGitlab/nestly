using System.IdentityModel.Tokens.Jwt;
using Asp.Versioning;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Nestly.Application.Reviews;
using Nestly.BuildingBlocks.Extensions;

namespace Nestly.ConsumerApi.Controllers;

/// <summary>Customer review submission for a completed booking (SRS 11.16, 17, 24.8, tasks 85a-c).</summary>
[ApiController]
[ApiVersion(1)]
[Authorize]
[Route("api/v{version:apiVersion}/bookings/{bookingId:guid}/review")]
public class ReviewsController : ControllerBase
{
    private readonly IReviewService _reviewService;
    private readonly IValidator<SubmitReviewRequest> _submitValidator;

    public ReviewsController(IReviewService reviewService, IValidator<SubmitReviewRequest> submitValidator)
    {
        _reviewService = reviewService;
        _submitValidator = submitValidator;
    }

    /// <summary>Whether this booking is eligible for a review right now (SRS 11.16.1, 11.16.3, 24.8 "get eligible review booking").</summary>
    [HttpGet("eligibility")]
    [ProducesResponseType(typeof(ReviewEligibilityResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEligibility(Guid bookingId)
    {
        var result = await _reviewService.GetEligibilityAsync(CurrentCustomerId(), bookingId);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>The review already submitted for this booking, if any.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ReviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid bookingId)
    {
        var result = await _reviewService.GetByBookingAsync(CurrentCustomerId(), bookingId);
        if (result.IsFailure)
        {
            return result.ToProblemResult();
        }

        return result.Value is null ? NoContent() : Ok(result.Value);
    }

    /// <summary>Submits the booking's one primary review (SRS 24.8).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ReviewResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Submit(Guid bookingId, [FromBody] SubmitReviewRequest request)
    {
        var validation = await _submitValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _reviewService.SubmitAsync(CurrentCustomerId(), bookingId, request);
        return result.IsSuccess ? CreatedAtAction(nameof(Get), new { bookingId }, result.Value) : result.ToProblemResult();
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

using Asp.Versioning;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Nestly.Application.CustomerRatings;
using Nestly.BuildingBlocks.Extensions;
using Nestly.Infrastructure;

namespace Nestly.ProviderApi.Controllers;

/// <summary>
/// Provider-side rating of the customer on a completed job - the reverse
/// direction of consumer-api's <c>ReviewsController</c> (bidirectional
/// reviews). Every action is scoped to the caller's own provider id from the
/// JWT, same IDOR-safe pattern as <see cref="JobsController"/>.
/// </summary>
[ApiController]
[ApiVersion(1)]
[Authorize(AuthenticationSchemes = DependencyInjection.ProviderJwtBearerScheme)]
[Route("api/v{version:apiVersion}/jobs/{bookingId:guid}/customer-rating")]
public class CustomerRatingsController : ControllerBase
{
    private readonly ICustomerRatingService _ratingService;
    private readonly IValidator<SubmitCustomerRatingRequest> _submitValidator;

    public CustomerRatingsController(ICustomerRatingService ratingService, IValidator<SubmitCustomerRatingRequest> submitValidator)
    {
        _ratingService = ratingService;
        _submitValidator = submitValidator;
    }

    /// <summary>Whether this job is eligible for a rating right now.</summary>
    [HttpGet("eligibility")]
    [ProducesResponseType(typeof(CustomerRatingEligibilityResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEligibility(Guid bookingId)
    {
        var result = await _ratingService.GetEligibilityAsync(CurrentProviderId(), bookingId);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>The rating already submitted for this job, if any.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(CustomerRatingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid bookingId)
    {
        var result = await _ratingService.GetByBookingAsync(CurrentProviderId(), bookingId);
        if (result.IsFailure)
        {
            return result.ToProblemResult();
        }

        return result.Value is null ? NoContent() : Ok(result.Value);
    }

    /// <summary>Submits the job's one rating of the customer.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(CustomerRatingResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Submit(Guid bookingId, [FromBody] SubmitCustomerRatingRequest request)
    {
        var validation = await _submitValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _ratingService.SubmitAsync(CurrentProviderId(), bookingId, request);
        return result.IsSuccess ? CreatedAtAction(nameof(Get), new { bookingId }, result.Value) : result.ToProblemResult();
    }

    private Guid CurrentProviderId() =>
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

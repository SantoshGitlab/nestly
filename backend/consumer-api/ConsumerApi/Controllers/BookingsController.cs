using System.IdentityModel.Tokens.Jwt;
using Asp.Versioning;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Nestly.Application.Bookings;
using Nestly.BuildingBlocks.Extensions;

namespace Nestly.ConsumerApi.Controllers;

/// <summary>Booking (SRS 13). Every action is scoped to the caller's own customer id — never a route/body parameter.</summary>
[ApiController]
[ApiVersion(1)]
[Authorize]
[Route("api/v{version:apiVersion}/bookings")]
public class BookingsController : ControllerBase
{
    private readonly IBookingSummaryService _bookingSummaryService;
    private readonly IValidator<BookingSummaryRequest> _summaryValidator;

    public BookingsController(IBookingSummaryService bookingSummaryService, IValidator<BookingSummaryRequest> summaryValidator)
    {
        _bookingSummaryService = bookingSummaryService;
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

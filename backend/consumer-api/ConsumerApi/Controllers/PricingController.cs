using Asp.Versioning;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Nestly.Application.Pricing;
using Nestly.BuildingBlocks.Extensions;

namespace Nestly.ConsumerApi.Controllers;

/// <summary>Server-side authoritative price calculation (task 48, SRS 11.9.2). No auth - price is checked before login too.</summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/pricing")]
public class PricingController : ControllerBase
{
    private readonly IPriceCalculationService _priceCalculationService;
    private readonly IValidator<PriceCalculationRequest> _validator;

    public PricingController(IPriceCalculationService priceCalculationService, IValidator<PriceCalculationRequest> validator)
    {
        _priceCalculationService = priceCalculationService;
        _validator = validator;
    }

    /// <summary>Calculates the full price breakdown for a service+city+quantity+add-ons combination.</summary>
    [HttpPost("calculate")]
    [ProducesResponseType(typeof(PriceBreakdownResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Calculate([FromBody] PriceCalculationRequest request)
    {
        var validation = await _validator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            var modelState = new ModelStateDictionary();
            foreach (var error in validation.Errors)
            {
                modelState.AddModelError(error.PropertyName, error.ErrorMessage);
            }

            return ValidationProblem(modelState);
        }

        var result = await _priceCalculationService.CalculateAsync(request);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }
}

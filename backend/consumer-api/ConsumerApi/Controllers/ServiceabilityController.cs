using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Nestly.Application.Serviceability;
using Nestly.BuildingBlocks.Extensions;

namespace Nestly.ConsumerApi.Controllers;

/// <summary>
/// Public serviceability checks (SRS 11.4, 24.4). No auth - browsing must be
/// able to tell a customer a category/service isn't offered at their
/// location before they sign in or add anything (SRS 11.4.3).
/// </summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/serviceability")]
public class ServiceabilityController : ControllerBase
{
    private readonly IServiceabilityValidationService _serviceabilityValidationService;

    public ServiceabilityController(IServiceabilityValidationService serviceabilityValidationService)
    {
        _serviceabilityValidationService = serviceabilityValidationService;
    }

    /// <summary>Whether a category is serviceable in a city (SRS 12.9.2).</summary>
    [HttpGet("categories/{categoryId:guid}")]
    [ProducesResponseType(typeof(ServiceabilityResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> IsCategoryServiceable(Guid categoryId, [FromQuery] Guid cityId)
    {
        var result = await _serviceabilityValidationService.IsCategoryServiceableAsync(categoryId, cityId);
        return result.IsSuccess ? Ok(new ServiceabilityResponse(result.Value)) : result.ToProblemResult();
    }

    /// <summary>
    /// Whether a service is serviceable at a locality (SRS 12.9.2) - the
    /// dimension a customer's selected address naturally carries, resolved to
    /// its parent pincode the same way the slot APIs do.
    /// </summary>
    [HttpGet("services/{serviceId:guid}")]
    [ProducesResponseType(typeof(ServiceabilityResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> IsServiceServiceable(Guid serviceId, [FromQuery] Guid localityId)
    {
        var result = await _serviceabilityValidationService.IsServiceServiceableByLocalityAsync(serviceId, localityId);
        return result.IsSuccess ? Ok(new ServiceabilityResponse(result.Value)) : result.ToProblemResult();
    }
}

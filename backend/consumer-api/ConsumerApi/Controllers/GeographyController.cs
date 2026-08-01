using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Nestly.Application.Geography;
using Nestly.BuildingBlocks.Extensions;

namespace Nestly.ConsumerApi.Controllers;

/// <summary>Public geography lookups for location selection (SRS 11.1, 11.4.1). No auth - browsing needs no session.</summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/geography")]
public class GeographyController : ControllerBase
{
    private readonly IGeographyQueryService _geographyQueryService;

    public GeographyController(IGeographyQueryService geographyQueryService)
    {
        _geographyQueryService = geographyQueryService;
    }

    /// <summary>Active cities for a location picker (SRS 11.1 - "change city from homepage").</summary>
    [HttpGet("cities")]
    [ProducesResponseType(typeof(IReadOnlyList<CityResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListCities()
    {
        var cities = await _geographyQueryService.ListActiveCitiesAsync();
        return Ok(cities);
    }

    /// <summary>
    /// Localities within a city matching an optional name/pincode search term
    /// (SRS 11.4.1), so a customer can resolve a localityId without knowing
    /// it - required by the slot and serviceability APIs.
    /// </summary>
    [HttpGet("cities/{cityId:guid}/localities")]
    [EnableRateLimiting("search")]
    [ProducesResponseType(typeof(IReadOnlyList<LocalityResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SearchLocalities(Guid cityId, [FromQuery] string? search)
    {
        var result = await _geographyQueryService.SearchLocalitiesAsync(cityId, search);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }
}

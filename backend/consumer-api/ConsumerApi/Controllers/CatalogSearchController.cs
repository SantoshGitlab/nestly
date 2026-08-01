using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Nestly.Application.Catalog;
using Nestly.BuildingBlocks.Extensions;

namespace Nestly.ConsumerApi.Controllers;

/// <summary>Catalog search (task 42c, SRS 11.5-11.6, 24.3). No auth - anyone can search.</summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/catalog/search")]
public class CatalogSearchController : ControllerBase
{
    private readonly ICatalogSearchService _searchService;

    public CatalogSearchController(ICatalogSearchService searchService)
    {
        _searchService = searchService;
    }

    /// <summary>Search categories and services by name.</summary>
    [HttpGet]
    [EnableRateLimiting("search")]
    [ProducesResponseType(typeof(CatalogSearchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Search([FromQuery] string q)
    {
        var result = await _searchService.SearchAsync(q);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }
}

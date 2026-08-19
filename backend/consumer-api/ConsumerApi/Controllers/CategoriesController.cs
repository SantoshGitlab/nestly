using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Nestly.Application.Catalog;
using Nestly.BuildingBlocks.Extensions;

namespace Nestly.ConsumerApi.Controllers;

/// <summary>Public category catalog (task 41, SRS 11.1/11.5). No auth - anyone can browse.</summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/categories")]
public class CategoriesController : ControllerBase
{
    private readonly ICategoryQueryService _categoryQueryService;

    public CategoriesController(ICategoryQueryService categoryQueryService)
    {
        _categoryQueryService = categoryQueryService;
    }

    /// <summary>
    /// List active categories serviceable in a city (SRS 11.1), optionally
    /// narrowed to a specific pincode when the customer has picked an area
    /// rather than just a city (SRS 11.1.3).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CategorySummaryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> List([FromQuery] Guid cityId, [FromQuery] Guid? pincodeId)
    {
        var result = await _categoryQueryService.ListServiceableInCityAsync(cityId, pincodeId);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Category detail with its active services and their add-ons (SRS 11.5).</summary>
    [HttpGet("{slug}")]
    [ProducesResponseType(typeof(CategoryDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDetail(string slug)
    {
        var result = await _categoryQueryService.GetDetailBySlugAsync(slug);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }
}

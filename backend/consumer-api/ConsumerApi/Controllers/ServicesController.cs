using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Nestly.Application.Catalog;
using Nestly.BuildingBlocks.Extensions;

namespace Nestly.ConsumerApi.Controllers;

/// <summary>Public service catalog (tasks 42a/42b, SRS 11.5-11.6). No auth - anyone can browse.</summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/services")]
public class ServicesController : ControllerBase
{
    private readonly IServiceQueryService _serviceQueryService;

    public ServicesController(IServiceQueryService serviceQueryService)
    {
        _serviceQueryService = serviceQueryService;
    }

    /// <summary>Services within a category (SRS 11.5.3).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ServiceListItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> List([FromQuery] Guid categoryId)
    {
        var result = await _serviceQueryService.ListByCategoryAsync(categoryId);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Service detail: inclusions/exclusions/add-ons/policies/FAQs (SRS 11.6.1).</summary>
    [HttpGet("{slug}")]
    [ProducesResponseType(typeof(ServiceDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDetail(string slug)
    {
        var result = await _serviceQueryService.GetDetailBySlugAsync(slug);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Rating summary and recent reviews for the service detail page (SRS 11.6.1 "Reviews and rating summary").</summary>
    [HttpGet("{slug}/reviews-summary")]
    [ProducesResponseType(typeof(ServiceReviewSummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetReviewsSummary(string slug)
    {
        var result = await _serviceQueryService.GetReviewSummaryBySlugAsync(slug);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }
}

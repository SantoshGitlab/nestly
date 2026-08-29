using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nestly.Application.Landing;
using Nestly.BuildingBlocks.Extensions;
using Nestly.Domain;
using Nestly.Infrastructure;

namespace Nestly.AdminApi.Controllers;

/// <summary>
/// Admin curation of the customer home page's three configurable sections.
/// Gated behind the existing "cms" module rather than "catalog": this picks
/// which catalog entries to merchandise, it does not create or edit them, and
/// it sits alongside banners/pages as home-page content.
///
/// Every write replaces a whole section (PUT, not POST/DELETE per row) so a
/// repeated save is idempotent and the submitted order is the display order.
/// </summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/admin/landing")]
[Authorize(AuthenticationSchemes = DependencyInjection.AdminJwtBearerScheme)]
public class LandingController : ControllerBase
{
    private const string ReadPolicy = AdminModules.Cms + ".read";
    private const string WritePolicy = AdminModules.Cms + ".write";

    private readonly ILandingManagementService _landingManagementService;

    public LandingController(ILandingManagementService landingManagementService)
    {
        _landingManagementService = landingManagementService;
    }

    /// <summary>The full curation config - all three sections in one call for the admin screen.</summary>
    [HttpGet]
    [Authorize(Policy = ReadPolicy)]
    [ProducesResponseType(typeof(LandingConfigResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetConfig() => Ok(await _landingManagementService.GetConfigAsync());

    /// <summary>Replaces the "New &amp; Trending" sub-category picks.</summary>
    [HttpPut("new-and-trending")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateNewAndTrending([FromBody] UpdateNewAndTrendingRequest request)
    {
        var result = await _landingManagementService.UpdateNewAndTrendingAsync(request);
        return result.IsSuccess ? NoContent() : result.ToProblemResult();
    }

    /// <summary>Replaces the "Most Booked Services" picks.</summary>
    [HttpPut("most-booked")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateMostBooked([FromBody] UpdateMostBookedRequest request)
    {
        var result = await _landingManagementService.UpdateMostBookedAsync(request);
        return result.IsSuccess ? NoContent() : result.ToProblemResult();
    }

    /// <summary>Replaces one category strip's service picks (max 5, all belonging to that category).</summary>
    [HttpPut("category-sections/{categoryId:guid}")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateCategorySection(Guid categoryId, [FromBody] UpdateCategorySectionRequest request)
    {
        var result = await _landingManagementService.UpdateCategorySectionAsync(categoryId, request);
        return result.IsSuccess ? NoContent() : result.ToProblemResult();
    }
}

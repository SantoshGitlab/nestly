using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Nestly.Application.Cms;

namespace Nestly.ConsumerApi.Controllers;

/// <summary>
/// Public static pages (SRS 12.16.1/12.16.2) - Terms &amp; Conditions, Privacy
/// Policy, Refund/Cancellation Policy, Contact Us and any other admin-authored
/// page. No auth - anyone can read a published page, same as
/// <see cref="BannersController"/>/<see cref="LandingController"/>. Only a
/// live (published, within its publish window) page is ever returned; admin
/// CRUD lives in admin-api's <c>CmsPagesController</c>.
/// </summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/cms/pages")]
public class CmsPagesController : ControllerBase
{
    private readonly ICmsPageQueryService _cmsPageQueryService;

    public CmsPagesController(ICmsPageQueryService cmsPageQueryService)
    {
        _cmsPageQueryService = cmsPageQueryService;
    }

    /// <summary>The live page at this slug. 404 covers "no such page", "still a draft" and "outside its publish window" identically - a customer app has no business telling those apart.</summary>
    [HttpGet("{slug}")]
    [ProducesResponseType(typeof(CmsPageContentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBySlug(string slug)
    {
        var page = await _cmsPageQueryService.GetLiveBySlugAsync(slug);
        return page is null ? NotFound() : Ok(page);
    }
}

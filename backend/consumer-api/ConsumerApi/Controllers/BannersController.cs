using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Nestly.Application.Cms;

namespace Nestly.ConsumerApi.Controllers;

/// <summary>
/// Public storefront banners (SRS 11.1.2/11.1.3 "home banner shall be
/// admin-configurable"). No auth - anyone loading the home page reads these,
/// same as <see cref="CategoriesController"/>. Only live, publish-windowed
/// banners are returned; admin CRUD lives in admin-api's BannersController.
/// </summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/banners")]
public class BannersController : ControllerBase
{
    private readonly IBannerQueryService _bannerQueryService;

    public BannersController(IBannerQueryService bannerQueryService)
    {
        _bannerQueryService = bannerQueryService;
    }

    /// <summary>The banners currently live for the home page, ordered for display. Empty array when none are live.</summary>
    [HttpGet("home")]
    [ProducesResponseType(typeof(IReadOnlyList<HomeBannerResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListHome() => Ok(await _bannerQueryService.ListLiveHomeBannersAsync());
}

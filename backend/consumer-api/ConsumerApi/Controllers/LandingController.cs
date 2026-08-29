using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Nestly.Application.Landing;

namespace Nestly.ConsumerApi.Controllers;

/// <summary>
/// The admin-curated home page sections ("New &amp; Trending", "Most Booked
/// Services" and the per-category strips). No auth - the home page is public,
/// same as <see cref="CategoriesController"/>.
///
/// The hero's category rail is NOT served here: it is location-dependent and
/// already covered by <c>GET /categories?cityId=</c>.
/// </summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/landing")]
public class LandingController : ControllerBase
{
    private readonly ILandingQueryService _landingQueryService;

    public LandingController(ILandingQueryService landingQueryService)
    {
        _landingQueryService = landingQueryService;
    }

    /// <summary>Every curated section in one call; unconfigured sections come back empty rather than absent.</summary>
    [HttpGet("home")]
    [ProducesResponseType(typeof(HomeLandingResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHome() => Ok(await _landingQueryService.GetHomeAsync());
}

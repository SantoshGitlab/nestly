using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Nestly.Application.Settings;
using Nestly.BuildingBlocks.Extensions;

namespace Nestly.ProviderApi.Controllers;

/// <summary>
/// Public provider-facing feature flags (SRS 12.19 "Feature flags"). No auth
/// - this gates navigation/UI before or without a session, same reasoning as
/// <see cref="GeographyController"/>. Projects the admin-only
/// <see cref="FeatureFlagSettings"/> group down to
/// <see cref="ProviderFeatureFlagsResponse"/> - never the full admin
/// settings shape, which would leak customer-side flags to an
/// unauthenticated caller.
/// </summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/feature-flags")]
public class FeatureFlagsController : ControllerBase
{
    private readonly ISystemSettingsService _settingsService;

    public FeatureFlagsController(ISystemSettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ProviderFeatureFlagsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get()
    {
        var result = await _settingsService.GetFeatureFlagSettingsAsync(HttpContext.RequestAborted);
        if (result.IsFailure)
        {
            return result.ToProblemResult();
        }

        FeatureFlagSettings feature = result.Value;
        var response = new ProviderFeatureFlagsResponse(
            feature.RatingsPageEnabled,
            feature.CalendarViewEnabled,
            feature.EarningsLedgerEnabled,
            feature.OffersScreenEnabled);

        return Ok(response);
    }
}

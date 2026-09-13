using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Nestly.Application.Settings;
using Nestly.BuildingBlocks.Extensions;

namespace Nestly.ConsumerApi.Controllers;

/// <summary>
/// Public customer-facing feature flags (SRS 12.19 "Feature flags"). No auth
/// - this gates navigation/UI before or without a session, same reasoning as
/// <see cref="GeographyController"/>. Projects the admin-only
/// <see cref="FeatureFlagSettings"/> group down to
/// <see cref="CustomerFeatureFlagsResponse"/> plus the pre-existing coupons
/// flag (<see cref="CouponSettings.CouponsEnabled"/>) - never the full
/// admin settings shape, which would leak provider-side flags to an
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
    [ProducesResponseType(typeof(CustomerFeatureFlagsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get()
    {
        var featureResult = await _settingsService.GetFeatureFlagSettingsAsync(HttpContext.RequestAborted);
        if (featureResult.IsFailure)
        {
            return featureResult.ToProblemResult();
        }

        var couponResult = await _settingsService.GetCouponSettingsAsync(HttpContext.RequestAborted);
        if (couponResult.IsFailure)
        {
            return couponResult.ToProblemResult();
        }

        FeatureFlagSettings feature = featureResult.Value;
        var response = new CustomerFeatureFlagsResponse(
            feature.WalletEnabled,
            couponResult.Value.CouponsEnabled,
            feature.ReferralsEnabled,
            feature.AmcSubscriptionsEnabled,
            feature.ServiceRatingsEnabled,
            feature.BookingHelpLinkEnabled);

        return Ok(response);
    }
}

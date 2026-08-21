using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nestly.Application.ProviderReferral;
using Nestly.BuildingBlocks.Extensions;

namespace Nestly.ProviderApi.Controllers;

/// <summary>
/// Refer &amp; Earn screen for providers (PROVIDER-REFERRAL.md): the caller's
/// own referral code/share link/lifetime stats, and their own referral
/// history. Mirrors consumer-api's <c>ReferralController</c> exactly - every
/// action is scoped to the caller's own provider id.
/// </summary>
[ApiController]
[ApiVersion(1)]
[Authorize]
[Route("api/v{version:apiVersion}/referral")]
public class ReferralController : ControllerBase
{
    private readonly IProviderReferralProviderService _referralProviderService;

    public ReferralController(IProviderReferralProviderService referralProviderService)
    {
        _referralProviderService = referralProviderService;
    }

    /// <summary>Code (lazily generated on first call), share link, and lifetime stats.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ProviderReferralSummaryResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Summary() => Ok(await _referralProviderService.GetSummaryAsync(CurrentProviderId()));

    /// <summary>This provider's own referrals as referrer, newest first.</summary>
    [HttpGet("history")]
    [ProducesResponseType(typeof(IReadOnlyList<ProviderReferralHistoryItemResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> History() => Ok(await _referralProviderService.GetHistoryAsync(CurrentProviderId()));

    private Guid CurrentProviderId() =>
        User.GetSubjectId();
}

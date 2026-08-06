using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nestly.Application.Referral;
using Nestly.BuildingBlocks.Extensions;

namespace Nestly.ConsumerApi.Controllers;

/// <summary>
/// Refer &amp; Earn screen (REFERRAL.md, task 168): the caller's own referral
/// code/share link/lifetime stats, and their own referral history. Every
/// action is scoped to the caller's own customer id, same pattern as
/// <see cref="WalletController"/>.
/// </summary>
[ApiController]
[ApiVersion(1)]
[Authorize]
[Route("api/v{version:apiVersion}/referral")]
public class ReferralController : ControllerBase
{
    private readonly IReferralCustomerService _referralCustomerService;

    public ReferralController(IReferralCustomerService referralCustomerService)
    {
        _referralCustomerService = referralCustomerService;
    }

    /// <summary>Code (lazily generated on first call), share link, and lifetime stats (REFERRAL.md "GET /me/referral").</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ReferralSummaryResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Summary() => Ok(await _referralCustomerService.GetSummaryAsync(CurrentCustomerId()));

    /// <summary>This customer's own referrals as referrer, newest first (REFERRAL.md "GET /me/referral/history").</summary>
    [HttpGet("history")]
    [ProducesResponseType(typeof(IReadOnlyList<ReferralHistoryItemResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> History() => Ok(await _referralCustomerService.GetHistoryAsync(CurrentCustomerId()));

    private Guid CurrentCustomerId() =>
        User.GetSubjectId();
}

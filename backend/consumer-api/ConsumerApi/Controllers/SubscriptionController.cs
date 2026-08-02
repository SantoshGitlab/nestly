using System.IdentityModel.Tokens.Jwt;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nestly.Application.Subscriptions;
using Nestly.BuildingBlocks.Extensions;

namespace Nestly.ConsumerApi.Controllers;

/// <summary>
/// Customer-facing subscription flow (PRODUCT-ENHANCEMENTS.md #1, task 181):
/// browse plans, subscribe, cancel, view active subscription. Every action
/// scoped to the caller's own customer id, same pattern as
/// <see cref="ReferralController"/>/<see cref="WalletController"/>.
/// </summary>
[ApiController]
[ApiVersion(1)]
[Authorize]
[Route("api/v{version:apiVersion}/subscription")]
public class SubscriptionController : ControllerBase
{
    private readonly ICustomerSubscriptionService _subscriptionService;

    public SubscriptionController(ICustomerSubscriptionService subscriptionService)
    {
        _subscriptionService = subscriptionService;
    }

    /// <summary>Every plan currently open to new subscribers.</summary>
    [HttpGet("plans")]
    [ProducesResponseType(typeof(IReadOnlyList<SubscriptionPlanBrowseResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> BrowsePlans() => Ok(await _subscriptionService.BrowsePlansAsync());

    /// <summary>The caller's current live subscription and remaining benefits, or 204 if they have none.</summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(MySubscriptionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MySubscription()
    {
        var subscription = await _subscriptionService.GetMyCurrentSubscriptionAsync(CurrentCustomerId());
        return subscription is null ? NoContent() : Ok(subscription);
    }

    [HttpPost("subscribe")]
    [ProducesResponseType(typeof(MySubscriptionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Subscribe([FromBody] SubscribeRequest request)
    {
        var result = await _subscriptionService.SubscribeAsync(CurrentCustomerId(), request);
        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : result.ToProblemResult();
    }

    [HttpPost("{subscriptionId:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(Guid subscriptionId)
    {
        var result = await _subscriptionService.CancelAsync(CurrentCustomerId(), subscriptionId);
        return result.IsSuccess ? NoContent() : result.ToProblemResult();
    }

    private Guid CurrentCustomerId() =>
        Guid.Parse(User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);
}

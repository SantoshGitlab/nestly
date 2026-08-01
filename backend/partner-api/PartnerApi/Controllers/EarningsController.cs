using System.IdentityModel.Tokens.Jwt;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nestly.Application.PartnerEarnings;
using Nestly.Application.PartnerManagement;
using Nestly.BuildingBlocks.Extensions;
using Nestly.Domain;
using Nestly.Infrastructure;

namespace Nestly.PartnerApi.Controllers;

/// <summary>
/// Partner earnings and payouts (task 149c, PARTNER.md API surface
/// "Earnings" - summary, ledger, payouts list/detail), wired to a real
/// <see cref="IPartnerEarningsService"/> backed by the ledger/payout
/// entities task 148 introduced. Every action is scoped to the caller's own
/// partner id taken from the JWT (SRS 28.3 IDOR), same pattern as
/// <see cref="ProfileController"/>.
/// </summary>
[ApiController]
[ApiVersion(1)]
[Authorize(AuthenticationSchemes = DependencyInjection.PartnerJwtBearerScheme)]
[Route("api/v{version:apiVersion}/earnings")]
public class EarningsController : ControllerBase
{
    private readonly IPartnerEarningsService _earningsService;

    public EarningsController(IPartnerEarningsService earningsService)
    {
        _earningsService = earningsService;
    }

    /// <summary>Rolled-up earnings summary (current balance) for the caller.</summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(PartnerEarningsSummaryResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummary()
    {
        var result = await _earningsService.GetSummaryAsync(CurrentPartnerId());
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Append-only earnings ledger entries for the caller, newest first.</summary>
    [HttpGet("ledger")]
    [ProducesResponseType(typeof(IReadOnlyList<PartnerEarningLedgerEntryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLedger()
    {
        var result = await _earningsService.GetLedgerAsync(CurrentPartnerId());
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Payout batches for the caller.</summary>
    [HttpGet("payouts")]
    [ProducesResponseType(typeof(PartnerPayoutSearchResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListPayouts([FromQuery] PartnerPayoutStatus? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _earningsService.ListPayoutsAsync(CurrentPartnerId(), status, page, pageSize);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>One payout's detail - 404s if it belongs to a different partner.</summary>
    [HttpGet("payouts/{id:guid}")]
    [ProducesResponseType(typeof(PartnerPayoutResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPayoutDetail(Guid id)
    {
        var result = await _earningsService.GetPayoutDetailAsync(CurrentPartnerId(), id);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    private Guid CurrentPartnerId() =>
        Guid.Parse(User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);
}

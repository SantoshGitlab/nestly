using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nestly.Application.Wallet;
using Nestly.BuildingBlocks.Extensions;

namespace Nestly.ConsumerApi.Controllers;

/// <summary>Wallet balance and ledger (SRS 11.17.1, 14.5, task 74c). Every action is scoped to the caller's own customer id.</summary>
[ApiController]
[ApiVersion(1)]
[Authorize]
[Route("api/v{version:apiVersion}/wallet")]
public class WalletController : ControllerBase
{
    private readonly IWalletService _walletService;

    public WalletController(IWalletService walletService)
    {
        _walletService = walletService;
    }

    [HttpGet("balance")]
    [ProducesResponseType(typeof(WalletBalanceResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Balance()
    {
        var result = await _walletService.GetBalanceAsync(CurrentCustomerId());
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    [HttpGet("ledger")]
    [ProducesResponseType(typeof(IReadOnlyList<WalletLedgerEntryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Ledger()
    {
        var result = await _walletService.GetLedgerAsync(CurrentCustomerId());
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    private Guid CurrentCustomerId() =>
        User.GetSubjectId();
}

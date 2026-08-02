using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Nestly.Application.NestlyCoins;
using Nestly.BuildingBlocks.Extensions;

namespace Nestly.ConsumerApi.Controllers;

/// <summary>
/// Public Nestly Coins program info (docs/NESTLY-COINS.md API SURFACE, task
/// 203). No auth - anyone can see the current earn rate/rules, same as
/// <see cref="CategoriesController"/>; a customer's own coins history is
/// already visible via the existing <c>WalletController</c>'s ledger
/// (GUIDELINES #4 - "no new endpoint, this is why reusing Wallet matters").
/// </summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/nestly-coins")]
public class NestlyCoinsController : ControllerBase
{
    private readonly INestlyCoinsCustomerService _customerService;

    public NestlyCoinsController(INestlyCoinsCustomerService customerService)
    {
        _customerService = customerService;
    }

    /// <summary>Current earn rate/rules, for in-app messaging ("earn coins on your next order"). 404 if the program isn't currently active.</summary>
    [HttpGet("program")]
    [ProducesResponseType(typeof(NestlyCoinsProgramPublicResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProgram()
    {
        var result = await _customerService.GetProgramAsync();
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }
}

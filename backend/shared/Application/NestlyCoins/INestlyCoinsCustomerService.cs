using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.NestlyCoins;

/// <summary>Customer-facing (public, unauthenticated) Nestly Coins surface (docs/NESTLY-COINS.md API SURFACE, task 203).</summary>
public interface INestlyCoinsCustomerService
{
    /// <summary>NotFound when the Customer-audience program doesn't exist or isn't active - there is nothing to advertise.</summary>
    Task<Result<NestlyCoinsProgramPublicResponse>> GetProgramAsync();
}

using Nestly.Application.NestlyCoins;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain.NestlyCoins;

namespace Nestly.Infrastructure.Services;

/// <inheritdoc cref="INestlyCoinsCustomerService"/>
public class NestlyCoinsCustomerService : INestlyCoinsCustomerService
{
    private readonly INestlyCoinsProgramConfigRepository _configRepository;

    public NestlyCoinsCustomerService(INestlyCoinsProgramConfigRepository configRepository)
    {
        _configRepository = configRepository;
    }

    public async Task<Result<NestlyCoinsProgramPublicResponse>> GetProgramAsync()
    {
        var config = await _configRepository.GetByAudienceAsync(NestlyCoinsAudience.Customer);
        if (config is null || !config.IsActive)
        {
            return Error.NotFound("NestlyCoinsProgram.NotActive", "Nestly Coins is not currently running.");
        }

        return Result.Success(new NestlyCoinsProgramPublicResponse(
            config.EarnRatePer100, config.MinimumOrderAmount, config.RequireReorder, config.ExpiryDays));
    }
}

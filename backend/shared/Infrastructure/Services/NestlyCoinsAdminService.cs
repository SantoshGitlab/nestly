using Nestly.Application;
using Nestly.Application.NestlyCoins;
using Nestly.Application.Wallet;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;
using Nestly.Domain.NestlyCoins;

namespace Nestly.Infrastructure.Services;

/// <inheritdoc cref="INestlyCoinsAdminService"/>
public class NestlyCoinsAdminService : INestlyCoinsAdminService
{
    private readonly INestlyCoinsProgramConfigRepository _configRepository;
    private readonly IWalletLedgerRepository _walletLedgerRepository;
    private readonly IPartnerEarningLedgerRepository _partnerEarningLedgerRepository;

    public NestlyCoinsAdminService(
        INestlyCoinsProgramConfigRepository configRepository,
        IWalletLedgerRepository walletLedgerRepository,
        IPartnerEarningLedgerRepository partnerEarningLedgerRepository)
    {
        _configRepository = configRepository;
        _walletLedgerRepository = walletLedgerRepository;
        _partnerEarningLedgerRepository = partnerEarningLedgerRepository;
    }

    public async Task<Result<NestlyCoinsProgramConfigResponse>> GetAsync(NestlyCoinsAudience audience)
    {
        var config = await _configRepository.GetByAudienceAsync(audience);
        if (config is null)
        {
            return Error.NotFound("NestlyCoinsProgramConfig.NotFound", $"No Nestly Coins program config exists yet for the {audience} audience.");
        }

        return Result.Success(ToResponse(config));
    }

    public async Task<Result<NestlyCoinsProgramConfigResponse>> UpsertAsync(NestlyCoinsAudience audience, NestlyCoinsProgramConfigUpsertRequest request, Guid adminUserId)
    {
        var config = await _configRepository.GetByAudienceAsync(audience);

        try
        {
            if (config is null)
            {
                config = new NestlyCoinsProgramConfig(
                    Guid.NewGuid(), audience, request.EarnRatePer100, request.MinimumOrderAmount, request.RequireReorder,
                    request.MaxCoinsPerMonth, request.ExpiryDays, request.ClawbackWindowDays, request.IsActive);
                await _configRepository.AddAsync(config);
            }
            else
            {
                config.Update(
                    request.EarnRatePer100, request.MinimumOrderAmount, request.RequireReorder,
                    request.MaxCoinsPerMonth, request.ExpiryDays, request.ClawbackWindowDays, request.IsActive, adminUserId);
                await _configRepository.UpdateAsync(config);
            }
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return Error.Validation("NestlyCoinsProgramConfig.Invalid", ex.Message);
        }

        return Result.Success(ToResponse(config));
    }

    public async Task<NestlyCoinsReportResponse> GetReportAsync(NestlyCoinsAudience audience, DateTime fromUtc, DateTime toUtc)
    {
        decimal issued, clawedBack;

        if (audience == NestlyCoinsAudience.Customer)
        {
            issued = await _walletLedgerRepository.SumBySourceTypeInRangeAsync(WalletSourceType.NestlyCoinsReward, WalletEntryType.Credit, fromUtc, toUtc);
            clawedBack = await _walletLedgerRepository.SumBySourceTypeInRangeAsync(WalletSourceType.NestlyCoinsClawback, WalletEntryType.Debit, fromUtc, toUtc);
        }
        else
        {
            issued = await _partnerEarningLedgerRepository.SumBySourceTypeInRangeAsync(PartnerEarningSourceType.NestlyCoinsReward, PartnerEarningEntryType.Credit, fromUtc, toUtc);
            clawedBack = await _partnerEarningLedgerRepository.SumBySourceTypeInRangeAsync(PartnerEarningSourceType.NestlyCoinsClawback, PartnerEarningEntryType.Debit, fromUtc, toUtc);
        }

        return new NestlyCoinsReportResponse(audience, fromUtc, toUtc, issued, clawedBack, issued - clawedBack);
    }

    private static NestlyCoinsProgramConfigResponse ToResponse(NestlyCoinsProgramConfig config) => new(
        config.Id, config.Audience, config.EarnRatePer100, config.MinimumOrderAmount, config.RequireReorder,
        config.MaxCoinsPerMonth, config.ExpiryDays, config.ClawbackWindowDays, config.IsActive,
        config.UpdatedAtUtc, config.UpdatedByAdminUserId);
}

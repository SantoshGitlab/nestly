using Nestly.BuildingBlocks.Results;
using Nestly.Domain.NestlyCoins;

namespace Nestly.Application.NestlyCoins;

/// <summary>Admin config get/update + report for Nestly Coins (docs/NESTLY-COINS.md API SURFACE, task 202).</summary>
public interface INestlyCoinsAdminService
{
    /// <summary>NotFound when this audience has never been configured yet (see NestlyCoinsProgramConfigUpsertRequest's doc comment).</summary>
    Task<Result<NestlyCoinsProgramConfigResponse>> GetAsync(NestlyCoinsAudience audience);

    Task<Result<NestlyCoinsProgramConfigResponse>> UpsertAsync(NestlyCoinsAudience audience, NestlyCoinsProgramConfigUpsertRequest request, Guid adminUserId);

    Task<NestlyCoinsReportResponse> GetReportAsync(NestlyCoinsAudience audience, DateTime fromUtc, DateTime toUtc);
}

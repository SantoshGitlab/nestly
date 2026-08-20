using Nestly.Application.ProviderReferral;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;

namespace Nestly.Infrastructure.Services;

/// <summary>See <see cref="IProviderReferralProgramConfigAdminService"/>. Mirrors ReferralProgramConfigAdminService.</summary>
public class ProviderReferralProgramConfigAdminService : IProviderReferralProgramConfigAdminService
{
    private readonly IProviderReferralProgramConfigRepository _configRepository;

    public ProviderReferralProgramConfigAdminService(IProviderReferralProgramConfigRepository configRepository)
    {
        _configRepository = configRepository;
    }

    public async Task<Result<ProviderReferralProgramConfigResponse>> GetAsync()
    {
        var config = await _configRepository.GetAsync();
        if (config is null)
        {
            return Error.NotFound("ProviderReferralProgramConfig.NotFound", "No provider referral program config exists yet.");
        }

        return Result.Success(ToResponse(config));
    }

    public async Task<Result<ProviderReferralProgramConfigResponse>> UpdateAsync(ProviderReferralProgramConfigUpdateRequest request, Guid adminUserId)
    {
        var config = await _configRepository.GetAsync();
        if (config is null)
        {
            return Error.NotFound("ProviderReferralProgramConfig.NotFound", "No provider referral program config exists yet.");
        }

        try
        {
            config.Update(
                request.ReferrerRewardValue,
                request.RefereeRewardValue,
                request.QualifyingCompletedJobsCount,
                request.ReferralExpiryDays,
                request.MaxReferralsPerProvider,
                request.IsActive,
                adminUserId);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return Error.Validation("ProviderReferralProgramConfig.Invalid", ex.Message);
        }

        await _configRepository.UpdateAsync(config);
        return Result.Success(ToResponse(config));
    }

    private static ProviderReferralProgramConfigResponse ToResponse(ProviderReferralProgramConfig config) => new(
        config.Id, config.ReferrerRewardValue, config.RefereeRewardValue, config.QualifyingCompletedJobsCount,
        config.ReferralExpiryDays, config.MaxReferralsPerProvider, config.IsActive, config.UpdatedAtUtc);
}

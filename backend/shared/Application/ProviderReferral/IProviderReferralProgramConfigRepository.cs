using Nestly.Domain;

namespace Nestly.Application.ProviderReferral;

/// <summary>Single-mutable-row config (see ProviderReferralProgramConfig's doc comment) - no "list" or "by id" lookups, there is exactly one row.</summary>
public interface IProviderReferralProgramConfigRepository
{
    Task<ProviderReferralProgramConfig?> GetAsync();

    Task UpdateAsync(ProviderReferralProgramConfig config);
}

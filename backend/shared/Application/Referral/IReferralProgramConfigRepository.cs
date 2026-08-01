using Nestly.Domain;

namespace Nestly.Application.Referral;

/// <summary>Single-mutable-row config (see ReferralProgramConfig's doc comment) - no "list" or "by id" lookups, there is exactly one row.</summary>
public interface IReferralProgramConfigRepository
{
    Task<ReferralProgramConfig?> GetAsync();

    Task UpdateAsync(ReferralProgramConfig config);
}

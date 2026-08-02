using Nestly.Domain.NestlyCoins;

namespace Nestly.Application.NestlyCoins;

/// <summary>One row per <see cref="NestlyCoinsAudience"/> (see NestlyCoinsProgramConfig's doc comment) - unlike ReferralProgramConfig's true singleton, so lookups are keyed by audience.</summary>
public interface INestlyCoinsProgramConfigRepository
{
    /// <summary>Null when this audience has never been configured - callers must treat that the same as "coins disabled for this side" (no default values are invented; an admin must explicitly configure and activate each side, task 202).</summary>
    Task<NestlyCoinsProgramConfig?> GetByAudienceAsync(NestlyCoinsAudience audience);

    Task<IReadOnlyList<NestlyCoinsProgramConfig>> ListAsync();

    Task AddAsync(NestlyCoinsProgramConfig config);

    Task UpdateAsync(NestlyCoinsProgramConfig config);
}

using Nestly.BuildingBlocks.Results;
using Nestly.Domain;

namespace Nestly.Application.Identity;

/// <summary>
/// Extension point for a future MFA challenge (SRS 12.1.1 "Optional MFA",
/// task 95f). Deliberately scoped as a hook only, not a full MFA
/// implementation — the default DI registration is a no-op that always
/// succeeds, so today's login flow behaves exactly as if MFA did not exist.
/// A real provider (TOTP, SMS, ...) can be swapped in later purely via DI
/// registration; <see cref="Nestly.Application.Identity.IAdminLoginService"/>'s
/// caller never has to change.
/// </summary>
public interface IAdminMfaChallengeProvider
{
    /// <summary>
    /// Called after password verification succeeds, before a session is
    /// issued. A failing result blocks login just like a wrong password would.
    /// </summary>
    Task<Result> VerifyAsync(AdminUser adminUser, CancellationToken cancellationToken = default);
}

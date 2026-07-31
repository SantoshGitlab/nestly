namespace Nestly.Application.Identity;

/// <summary>
/// Non-standard claim types <see cref="IAdminTokenService"/> issues on top of
/// the standard <c>sub</c>/<c>email</c> ones, for RBAC enforcement (SRS 12.2,
/// tasks 96b-96c). Named constants rather than inline string literals so the
/// issuer (token service) and every consumer (authorization handler,
/// controllers reading claims) cannot drift apart.
/// </summary>
public static class AdminClaimTypes
{
    /// <summary>
    /// One claim of this type per granted permission code (e.g. "catalog.write") —
    /// multiple claims of the same type rather than one delimited value, so
    /// <c>ClaimsPrincipal.HasClaim(AdminClaimTypes.Permission, code)</c> and
    /// ASP.NET Core's built-in claim-based policies work without custom parsing.
    /// </summary>
    public const string Permission = "permission";

    /// <summary>The admin's assigned <see cref="Nestly.Domain.AdminRole"/> name, for display/audit — not itself checked by authorization (permission claims are).</summary>
    public const string AdminRoleName = "admin_role";
}

namespace Nestly.Infrastructure.Options;

/// <summary>
/// Strongly typed binding of the "PartnerAccount" configuration section: the
/// login-lockout policy for partners, mirroring <see cref="AccountOptions"/>'s
/// lockout fields. No password-auth/email-uniqueness fields - PARTNER.md's
/// API surface has no password login for partners, so those
/// <see cref="AccountOptions"/> fields have no partner equivalent (yet).
/// </summary>
public class PartnerAccountOptions
{
    public const string SectionName = "PartnerAccount";

    /// <summary>
    /// Consecutive failed login attempts against one mobile number within
    /// <see cref="LockoutWindowMinutes"/> before further attempts are
    /// refused (mirrors <see cref="AccountOptions.MaxFailedLoginAttempts"/>).
    /// </summary>
    public int MaxFailedLoginAttempts { get; set; } = 5;

    public int LockoutWindowMinutes { get; set; } = 15;
}

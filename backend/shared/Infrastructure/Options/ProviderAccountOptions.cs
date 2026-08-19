namespace Nestly.Infrastructure.Options;

/// <summary>
/// Strongly typed binding of the "ProviderAccount" configuration section:
/// the login-lockout policy and (task 372) email+password auth toggles for
/// providers, mirroring <see cref="AccountOptions"/> field-for-field.
/// </summary>
public class ProviderAccountOptions
{
    public const string SectionName = "ProviderAccount";

    /// <summary>
    /// Consecutive failed login attempts against one identifier (mobile or
    /// email) within <see cref="LockoutWindowMinutes"/> before further
    /// attempts are refused (mirrors <see cref="AccountOptions.MaxFailedLoginAttempts"/>).
    /// </summary>
    public int MaxFailedLoginAttempts { get; set; } = 5;

    public int LockoutWindowMinutes { get; set; } = 15;

    /// <summary>
    /// When false, a registration request supplying a password is rejected
    /// rather than silently ignored — mobile+OTP remains available either way
    /// (mirrors <see cref="AccountOptions.PasswordAuthEnabled"/>).
    /// </summary>
    public bool PasswordAuthEnabled { get; set; } = true;

    /// <summary>Whether two providers may share the same email address.</summary>
    public bool RequireUniqueEmail { get; set; } = true;
}

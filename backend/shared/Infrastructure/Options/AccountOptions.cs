namespace Nestly.Infrastructure.Options;

/// <summary>
/// Strongly typed binding of the "Identity" configuration section: the
/// account-management rules SRS 11.2.1 calls "configurable" rather than
/// fixed (email+password mode, email uniqueness).
/// </summary>
public class AccountOptions
{
    public const string SectionName = "Identity";

    /// <summary>
    /// When false, a registration request supplying a password is rejected
    /// rather than silently ignored — mobile+OTP remains available either way.
    /// </summary>
    public bool PasswordAuthEnabled { get; set; } = true;

    /// <summary>Whether two customers may share the same email address.</summary>
    public bool RequireUniqueEmail { get; set; } = true;

    /// <summary>
    /// Consecutive failed login attempts against one identifier (mobile or
    /// email) within <see cref="LockoutWindowMinutes"/> before further
    /// attempts against it are refused outright, regardless of whether the
    /// next one would have been correct (SRS 11.2.2 lockout policy).
    /// </summary>
    public int MaxFailedLoginAttempts { get; set; } = 5;

    public int LockoutWindowMinutes { get; set; } = 15;
}

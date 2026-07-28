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
}

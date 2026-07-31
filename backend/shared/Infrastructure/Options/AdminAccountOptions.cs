namespace Nestly.Infrastructure.Options;

/// <summary>
/// Strongly typed binding of the "AdminIdentity" configuration section:
/// login-lockout rules for the admin panel (SRS 12.1.1, task 95d). Kept
/// separate from <see cref="AccountOptions"/> (customer login) so the admin
/// policy can be tuned independently — a back-office account is a
/// higher-value target than any single customer's, which is why the default
/// lockout below is longer than the customer equivalent. Per-IP throttling
/// (task 95c) is handled separately by the "login" rate-limit policy
/// (<see cref="RateLimitOptions"/>), applied in each API's Program.cs.
/// </summary>
public class AdminAccountOptions
{
    public const string SectionName = "AdminIdentity";

    /// <summary>Consecutive failed attempts against one admin account before it locks.</summary>
    public int MaxFailedLoginAttempts { get; set; } = 5;

    /// <summary>
    /// How long a lockout lasts once triggered, absent an explicit
    /// administrative unlock (<c>AdminUser.Unlock</c> — the task 95d unlock path).
    /// </summary>
    public int LockoutMinutes { get; set; } = 30;
}

using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>Lifecycle state of a back-office operator account (SRS 12.2.1).</summary>
public enum AdminUserStatus
{
    Active,
    Inactive
}

/// <summary>
/// A back-office operator account (SRS 12.1, 12.2.1). Authenticates with
/// email + password, distinct from the customer identity model
/// (<see cref="CustomerAuthIdentity"/>) — admin accounts are provisioned by a
/// Super Admin rather than self-registered, and always carry a password
/// (mirrors <see cref="CustomerAuthIdentity"/>'s <c>PasswordHash</c> pattern
/// for the email+password provider).
/// </summary>
public class AdminUser : Entity<Guid>
{
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string FullName { get; private set; } = string.Empty;
    public AdminUserStatus Status { get; private set; }

    /// <summary>
    /// The <see cref="AdminRole"/> this account's permissions are drawn from
    /// (SRS 12.2.1 "Assign role(s)", task 96c). Null until a Super Admin
    /// assigns one (full role-assignment CRUD is task 97b) — an unassigned
    /// account authenticates but carries no permission claims, so it can act
    /// on nothing until a role is set.
    /// </summary>
    public Guid? RoleId { get; private set; }

    /// <summary>Consecutive failed logins since the last success or unlock (SRS 12.1.1, task 95d).</summary>
    public int FailedLoginAttempts { get; private set; }

    /// <summary>Set once <see cref="FailedLoginAttempts"/> crosses the configured threshold; null when not locked.</summary>
    public DateTime? LockedUntilUtc { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    protected AdminUser() { }

    public AdminUser(Guid id, string email, string passwordHash, string fullName) : base(id)
    {
        Email = string.IsNullOrWhiteSpace(email)
            ? throw new ArgumentException("Email is required.", nameof(email))
            : email;
        PasswordHash = string.IsNullOrWhiteSpace(passwordHash)
            ? throw new ArgumentException("Password hash is required.", nameof(passwordHash))
            : passwordHash;
        FullName = string.IsNullOrWhiteSpace(fullName)
            ? throw new ArgumentException("Full name is required.", nameof(fullName))
            : fullName;
        Status = AdminUserStatus.Active;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateProfile(string fullName)
    {
        FullName = string.IsNullOrWhiteSpace(fullName)
            ? throw new ArgumentException("Full name is required.", nameof(fullName))
            : fullName;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Applied after the admin re-verified control of the new address; this type only records the decision.</summary>
    public void ChangeEmail(string email)
    {
        Email = string.IsNullOrWhiteSpace(email)
            ? throw new ArgumentException("Email is required.", nameof(email))
            : email;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetPasswordHash(string passwordHash)
    {
        PasswordHash = string.IsNullOrWhiteSpace(passwordHash)
            ? throw new ArgumentException("Password hash is required.", nameof(passwordHash))
            : passwordHash;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Assigns (or clears, with null) the role this account's permissions come from.</summary>
    public void AssignRole(Guid? roleId)
    {
        RoleId = roleId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Activate()
    {
        if (Status == AdminUserStatus.Active) return;
        Status = AdminUserStatus.Active;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        if (Status == AdminUserStatus.Inactive) return;
        Status = AdminUserStatus.Inactive;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Whether a login lockout (task 95d) is currently in effect.</summary>
    public bool IsLockedOut(DateTime nowUtc) => LockedUntilUtc.HasValue && LockedUntilUtc.Value > nowUtc;

    /// <summary>
    /// Records one failed login attempt. Once <paramref name="maxFailedAttempts"/>
    /// is reached the account locks for <paramref name="lockoutDuration"/> from
    /// now — a further failure while already locked pushes the lockout out
    /// again rather than being ignored, so a continued attack cannot simply
    /// wait out an in-progress lockout (SRS 12.1.1).
    /// </summary>
    public void RegisterFailedLoginAttempt(int maxFailedAttempts, TimeSpan lockoutDuration, DateTime nowUtc)
    {
        FailedLoginAttempts++;
        if (FailedLoginAttempts >= maxFailedAttempts)
        {
            LockedUntilUtc = nowUtc.Add(lockoutDuration);
        }

        UpdatedAt = nowUtc;
    }

    /// <summary>A successful login clears the failure count and any active lockout.</summary>
    public void RegisterSuccessfulLogin(DateTime nowUtc) => ResetLockoutState(nowUtc);

    /// <summary>
    /// Administrative override that clears a lockout immediately instead of
    /// waiting for it to expire (task 95d's unlock path — e.g. a Super Admin
    /// confirming the failures were the owner mistyping their own password).
    /// </summary>
    public void Unlock(DateTime nowUtc) => ResetLockoutState(nowUtc);

    private void ResetLockoutState(DateTime nowUtc)
    {
        FailedLoginAttempts = 0;
        LockedUntilUtc = null;
        UpdatedAt = nowUtc;
    }
}

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
}

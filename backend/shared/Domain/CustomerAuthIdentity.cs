using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

public enum AuthProviderType
{
    MobileOtp,
    EmailPassword
}

/// <summary>
/// A single credential a customer can authenticate with (SRS 11.2.1: mobile+OTP
/// is always available, email+password is a configurable mode). A customer may
/// have more than one identity (e.g. mobile-OTP plus an added email+password).
/// </summary>
public class CustomerAuthIdentity : Entity<Guid>
{
    public Guid CustomerId { get; private set; }
    public AuthProviderType Provider { get; private set; }
    public string Identifier { get; private set; } = string.Empty;
    public string? PasswordHash { get; private set; }
    public bool IsPrimary { get; private set; }
    public DateTime CreatedAt { get; private set; }

    protected CustomerAuthIdentity() { }

    public CustomerAuthIdentity(Guid id, Guid customerId, AuthProviderType provider, string identifier, bool isPrimary)
        : base(id)
    {
        CustomerId = customerId;
        Provider = provider;
        Identifier = identifier ?? throw new ArgumentNullException(nameof(identifier));
        IsPrimary = isPrimary;
        CreatedAt = DateTime.UtcNow;
    }

    public void SetPasswordHash(string passwordHash) =>
        PasswordHash = passwordHash ?? throw new ArgumentNullException(nameof(passwordHash));

    public void MakePrimary() => IsPrimary = true;
}

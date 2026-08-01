using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>
/// A single credential a partner can authenticate with (PARTNER.md
/// "partner_auth_identity ... mirrors the customer auth tables"). Reuses
/// <see cref="AuthProviderType"/> - it is a generic provider-kind enum, not
/// customer-specific - so a future email+password mode for partners needs no
/// new type. Only <see cref="AuthProviderType.MobileOtp"/> is issued by the
/// registration flow today (PARTNER.md API surface lists no password login
/// for partners), mirroring <see cref="CustomerAuthIdentity"/>'s shape
/// exactly so it can be extended the same way if that changes.
/// </summary>
public class PartnerAuthIdentity : Entity<Guid>
{
    public Guid PartnerId { get; private set; }
    public AuthProviderType Provider { get; private set; }
    public string Identifier { get; private set; } = string.Empty;
    public string? PasswordHash { get; private set; }
    public bool IsPrimary { get; private set; }
    public DateTime CreatedAt { get; private set; }

    protected PartnerAuthIdentity() { }

    public PartnerAuthIdentity(Guid id, Guid partnerId, AuthProviderType provider, string identifier, bool isPrimary)
        : base(id)
    {
        PartnerId = partnerId;
        Provider = provider;
        Identifier = identifier ?? throw new ArgumentNullException(nameof(identifier));
        IsPrimary = isPrimary;
        CreatedAt = DateTime.UtcNow;
    }

    public void SetPasswordHash(string passwordHash) =>
        PasswordHash = passwordHash ?? throw new ArgumentNullException(nameof(passwordHash));

    public void MakePrimary() => IsPrimary = true;

    /// <summary>Repoints this credential after the partner re-verified a new mobile/email (mirrors <c>CustomerAuthIdentity.ChangeIdentifier</c>).</summary>
    public void ChangeIdentifier(string identifier) =>
        Identifier = string.IsNullOrWhiteSpace(identifier)
            ? throw new ArgumentException("Identifier is required.", nameof(identifier))
            : identifier;
}

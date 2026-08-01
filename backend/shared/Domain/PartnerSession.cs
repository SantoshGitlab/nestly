using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>
/// A partner's refresh-token session (PARTNER.md "partner_session ... mirrors
/// the customer auth tables"). Exact structural mirror of
/// <see cref="CustomerSession"/>, kept as its own table for the same
/// module-independence reason as <see cref="PartnerOtp"/>. The access token
/// itself is a stateless JWT and is not persisted; this row exists so a
/// refresh/logout can invalidate it.
/// </summary>
public class PartnerSession : Entity<Guid>
{
    public Guid PartnerId { get; private set; }
    public string RefreshTokenHash { get; private set; } = string.Empty;
    public DateTime IssuedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public string? DeviceInfo { get; private set; }
    public string? IpAddress { get; private set; }

    protected PartnerSession() { }

    public PartnerSession(Guid id, Guid partnerId, string refreshTokenHash, DateTime issuedAt, DateTime expiresAt,
        string? deviceInfo = null, string? ipAddress = null) : base(id)
    {
        PartnerId = partnerId;
        RefreshTokenHash = refreshTokenHash ?? throw new ArgumentNullException(nameof(refreshTokenHash));
        IssuedAt = issuedAt;
        ExpiresAt = expiresAt > issuedAt
            ? expiresAt
            : throw new ArgumentOutOfRangeException(nameof(expiresAt));
        DeviceInfo = deviceInfo;
        IpAddress = ipAddress;
    }

    public bool IsActive(DateTime asOfUtc) => RevokedAt is null && asOfUtc < ExpiresAt;

    public void Revoke() => RevokedAt = DateTime.UtcNow;
}

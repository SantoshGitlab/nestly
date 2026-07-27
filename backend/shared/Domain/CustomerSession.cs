using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>
/// A customer's refresh-token session (SRS 11.2.2: JWT access+refresh tokens,
/// session invalidation on logout). The access token itself is stateless JWT
/// and is not persisted; this row exists so a refresh/logout can invalidate it.
/// </summary>
public class CustomerSession : Entity<Guid>
{
    public Guid CustomerId { get; private set; }
    public string RefreshTokenHash { get; private set; } = string.Empty;
    public DateTime IssuedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public string? DeviceInfo { get; private set; }
    public string? IpAddress { get; private set; }

    protected CustomerSession() { }

    public CustomerSession(Guid id, Guid customerId, string refreshTokenHash, DateTime issuedAt, DateTime expiresAt,
        string? deviceInfo = null, string? ipAddress = null) : base(id)
    {
        CustomerId = customerId;
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

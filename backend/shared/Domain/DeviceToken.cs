using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>
/// A customer's registered device for push delivery (SRS 19.1 push channel,
/// task 156). <see cref="Token"/> is unique platform-wide, not per customer -
/// a device/app-install can be handed from one signed-in customer to
/// another (e.g. logout/login with a different account on the same phone),
/// so registering a token already owned by someone else reassigns it here
/// rather than failing (see <c>DeviceTokenService.RegisterAsync</c>).
/// </summary>
public class DeviceToken : Entity<Guid>
{
    public Guid CustomerId { get; private set; }
    public DevicePlatform Platform { get; private set; }
    public string Token { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTime RegisteredAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }

    protected DeviceToken() { }

    public DeviceToken(Guid id, Guid customerId, DevicePlatform platform, string token) : base(id)
    {
        CustomerId = customerId;
        Platform = platform;
        Token = string.IsNullOrWhiteSpace(token)
            ? throw new ArgumentException("Device token is required.", nameof(token))
            : token;
        IsActive = true;
        RegisteredAtUtc = DateTime.UtcNow;
    }

    /// <summary>Re-registers this token for <paramref name="customerId"/> and reactivates it - covers both "same customer re-registered" and "token handed to a different customer".</summary>
    public void ReRegister(Guid customerId, DevicePlatform platform)
    {
        CustomerId = customerId;
        Platform = platform;
        IsActive = true;
        RevokedAtUtc = null;
        RegisteredAtUtc = DateTime.UtcNow;
    }

    public void Revoke()
    {
        IsActive = false;
        RevokedAtUtc = DateTime.UtcNow;
    }
}

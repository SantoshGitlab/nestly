using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

public enum OtpPurpose
{
    Registration,
    Login,
    PasswordReset
}

/// <summary>
/// A single OTP challenge (SRS 11.2.1 registration, 11.2.2 login, 28.1 abuse
/// control). <see cref="AttemptCount"/> and <see cref="ExpiresAt"/> back the
/// retry-limit/expiry rules; the code itself is stored hashed, never plaintext.
/// </summary>
public class CustomerOtp : Entity<Guid>
{
    public Guid? CustomerId { get; private set; }
    public string Target { get; private set; } = string.Empty;
    public OtpPurpose Purpose { get; private set; }
    public string CodeHash { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }
    public DateTime? ConsumedAt { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTime CreatedAt { get; private set; }

    protected CustomerOtp() { }

    public CustomerOtp(Guid id, Guid? customerId, string target, OtpPurpose purpose, string codeHash, DateTime expiresAt)
        : base(id)
    {
        CustomerId = customerId;
        Target = target ?? throw new ArgumentNullException(nameof(target));
        Purpose = purpose;
        CodeHash = codeHash ?? throw new ArgumentNullException(nameof(codeHash));
        ExpiresAt = expiresAt;
        AttemptCount = 0;
        CreatedAt = DateTime.UtcNow;
    }

    public bool IsExpired(DateTime asOfUtc) => asOfUtc >= ExpiresAt;
    public bool IsConsumed => ConsumedAt is not null;

    public void RecordAttempt() => AttemptCount++;

    public void MarkConsumed() => ConsumedAt = DateTime.UtcNow;
}

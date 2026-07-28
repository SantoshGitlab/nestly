using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>
/// One login attempt, success or failure, keyed by the identifier the
/// customer tried to log in with (mobile or email) rather than a customer
/// id — a lockout has to apply before a failed attempt can even be tied to
/// a real account (SRS 11.2.2 lockout policy, 28.1 abuse control). This is
/// deliberately separate from <see cref="CustomerOtp.AttemptCount"/>, which
/// only bounds retries against one specific OTP: a new OTP resets that
/// counter, so it alone cannot stop someone from just requesting a fresh
/// OTP after each burst of wrong guesses.
/// </summary>
public class LoginAttempt : Entity<Guid>
{
    public string Identifier { get; private set; } = string.Empty;
    public bool Succeeded { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }

    protected LoginAttempt() { }

    public LoginAttempt(Guid id, string identifier, bool succeeded, DateTime occurredAtUtc) : base(id)
    {
        Identifier = identifier ?? throw new ArgumentNullException(nameof(identifier));
        Succeeded = succeeded;
        OccurredAtUtc = occurredAtUtc;
    }
}
